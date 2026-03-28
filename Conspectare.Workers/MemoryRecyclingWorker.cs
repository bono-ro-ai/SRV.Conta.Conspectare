using Conspectare.Services.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Standalone background service that monitors managed heap usage and triggers an
/// aggressive GC collection when the process appears idle and memory exceeds a threshold.
/// This is intentionally separate from <see cref="DistributedBackgroundService"/> because
/// it does not need distributed locking — each instance should recycle its own memory.
/// </summary>
public class MemoryRecyclingWorker : BackgroundService
{
    /// <summary>Heap size above which a GC collection will be considered.</summary>
    private const long DefaultHeapThresholdBytes = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Maximum heap change between two checks that is still classified as "idle".
    /// If more than 1 MB was allocated or freed since last check, the process is
    /// considered active and the GC is skipped.
    /// </summary>
    private const long IdleDeltaThresholdBytes = 1 * 1024 * 1024; // 1 MB

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);

    /// <summary>Minimum time between consecutive GC triggers to avoid thrashing.</summary>
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);

    private readonly ConspectareMetrics _metrics;
    private readonly ILogger<MemoryRecyclingWorker> _logger;
    private readonly long _heapThresholdBytes;

    private DateTime _lastRecycleUtc = DateTime.MinValue;
    private long _previousHeapBytes;

    /// <summary>
    /// Initialises the worker with metrics and logging dependencies.
    /// The heap threshold is set to <see cref="DefaultHeapThresholdBytes"/> (100 MB).
    /// </summary>
    public MemoryRecyclingWorker(
        ConspectareMetrics metrics,
        ILogger<MemoryRecyclingWorker> logger)
    {
        _metrics = metrics;
        _logger = logger;
        _heapThresholdBytes = DefaultHeapThresholdBytes;
    }

    /// <summary>
    /// Main loop. Checks heap conditions every <see cref="CheckInterval"/> and triggers
    /// a blocking, compacting GC when all three conditions are met: the heap is above
    /// the threshold, the process appears idle, and the cooldown period has elapsed.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Snapshot the initial heap so the first delta comparison is meaningful.
        _previousHeapBytes = GC.GetTotalMemory(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var currentHeap = GC.GetTotalMemory(false);
            var delta = Math.Abs(currentHeap - _previousHeapBytes);

            var isIdle = delta < IdleDeltaThresholdBytes;
            var isAboveThreshold = currentHeap > _heapThresholdBytes;
            var cooldownElapsed = DateTime.UtcNow - _lastRecycleUtc >= CooldownPeriod;

            // Update the baseline before any early-exit so the next check uses fresh data.
            _previousHeapBytes = currentHeap;

            // All three conditions must be true before forcing a collection.
            if (!isIdle || !isAboveThreshold || !cooldownElapsed)
                continue;

            _logger.LogInformation(
                "MemoryRecycling: idle detected, heap {HeapMB:F1} MB exceeds threshold {ThresholdMB:F1} MB — triggering GC",
                currentHeap / (1024.0 * 1024.0),
                _heapThresholdBytes / (1024.0 * 1024.0));

            // Generation 2, aggressive mode, blocking + compacting — this is intentionally
            // disruptive, but only fires when the service is idle and memory is high.
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            var afterHeap = GC.GetTotalMemory(true);
            _lastRecycleUtc = DateTime.UtcNow;
            _previousHeapBytes = afterHeap;

            _metrics.RecordMemoryRecyclingTriggered(currentHeap, afterHeap);

            _logger.LogInformation(
                "MemoryRecycling: GC completed, heap {BeforeMB:F1} MB -> {AfterMB:F1} MB (freed {FreedMB:F1} MB)",
                currentHeap / (1024.0 * 1024.0),
                afterHeap / (1024.0 * 1024.0),
                (currentHeap - afterHeap) / (1024.0 * 1024.0));
        }
    }
}
