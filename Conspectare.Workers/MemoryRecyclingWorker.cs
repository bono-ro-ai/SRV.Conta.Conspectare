using Conspectare.Services.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class MemoryRecyclingWorker : BackgroundService
{
    private const long DefaultHeapThresholdBytes = 100 * 1024 * 1024; // 100 MB
    private const long IdleDeltaThresholdBytes = 1 * 1024 * 1024; // 1 MB
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);
    private readonly ConspectareMetrics _metrics;
    private readonly ILogger<MemoryRecyclingWorker> _logger;
    private readonly long _heapThresholdBytes;
    private DateTime _lastRecycleUtc = DateTime.MinValue;
    private long _previousHeapBytes;
    public MemoryRecyclingWorker(
        ConspectareMetrics metrics,
        ILogger<MemoryRecyclingWorker> logger)
    {
        _metrics = metrics;
        _logger = logger;
        _heapThresholdBytes = DefaultHeapThresholdBytes;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
            _previousHeapBytes = currentHeap;
            if (!isIdle || !isAboveThreshold || !cooldownElapsed)
                continue;
            _logger.LogInformation(
                "MemoryRecycling: idle detected, heap {HeapMB:F1} MB exceeds threshold {ThresholdMB:F1} MB — triggering GC",
                currentHeap / (1024.0 * 1024.0),
                _heapThresholdBytes / (1024.0 * 1024.0));
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
