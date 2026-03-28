using System.Diagnostics;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHibernate;

namespace Conspectare.Workers;

/// <summary>
/// Abstract base class for all distributed background workers. Provides distributed locking
/// (so only one instance runs a given job at a time), adaptive back-off when there is no work,
/// optional pipeline-signal wake-up, and persistent execution audit records.
/// </summary>
public abstract class DistributedBackgroundService : BackgroundService
{
    // Hard cap on how far adaptive back-off is allowed to grow.
    private static readonly TimeSpan MaxAdaptiveInterval = TimeSpan.FromSeconds(10);

    // Returns whichever is larger: the cap or the configured Interval, so a worker whose
    // Interval is already longer than 10 s is never forced below its own cadence.
    private TimeSpan EffectiveMaxInterval => TimeSpan.FromMilliseconds(
        Math.Max(MaxAdaptiveInterval.TotalMilliseconds, Interval.TotalMilliseconds));

    private readonly IDistributedLock _lock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly IPipelineSignal _signal;
    private readonly string _instanceId;

    // Mutable; adjusted after each run via adaptive back-off logic.
    private TimeSpan _currentInterval;

    /// <summary>Gets the unique name used as the distributed-lock key and for logging.</summary>
    protected abstract string JobName { get; }

    /// <summary>Gets the nominal polling interval between job executions.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// When <see langword="true"/>, the job runs exactly once after <see cref="StartupDelay"/>
    /// then the hosted service exits. Defaults to <see langword="false"/>.
    /// </summary>
    protected virtual bool IsOneShot => false;

    /// <summary>Gets the delay before the very first execution. Defaults to 5 seconds.</summary>
    protected virtual TimeSpan StartupDelay => TimeSpan.FromSeconds(5);

    /// <summary>
    /// When set, the worker subscribes to this pipeline phase's signal so it can wake up
    /// immediately rather than waiting for the full polling interval.
    /// </summary>
    protected virtual string SignalStage => null;

    /// <summary>
    /// Initialises shared state. The <paramref name="signal"/> parameter is optional;
    /// workers that do not need event-driven wake-up may omit it.
    /// </summary>
    protected DistributedBackgroundService(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        IPipelineSignal signal = null)
    {
        _lock = distributedLock;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _signal = signal;

        // Combine hostname + random suffix so logs can identify which replica ran a job.
        _instanceId = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Main execution loop. Handles both the one-shot and recurring variants, and
    /// delegates to <see cref="RunWithLockAsync"/> for the actual work.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (IsOneShot)
        {
            await Task.Delay(StartupDelay, stoppingToken);
            await RunWithLockAsync(stoppingToken);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelay();
            _logger.LogInformation("{JobName}: next run in {Delay}", JobName, delay);

            try
            {
                // Prefer signal-based wake-up; fall back to a plain delay.
                if (_signal != null && SignalStage != null)
                    await _signal.WaitAsync(SignalStage, delay, stoppingToken);
                else
                    await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunWithLockAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Returns the delay to use before the next execution. Subclasses may override to
    /// implement custom scheduling. The default implementation applies adaptive back-off:
    /// on idle runs the interval doubles up to <see cref="EffectiveMaxInterval"/>.
    /// </summary>
    protected virtual TimeSpan ComputeDelay()
    {
        if (_currentInterval == TimeSpan.Zero)
            _currentInterval = Interval;

        return _currentInterval;
    }

    /// <summary>
    /// Override in each concrete worker to perform the actual job logic.
    /// Returns the number of items processed; returning 0 triggers adaptive back-off.
    /// </summary>
    protected abstract Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct);

    // Attempts to acquire the distributed lock and, if successful, runs the job, records
    // the result, and applies adaptive back-off based on whether any items were processed.
    private async Task RunWithLockAsync(CancellationToken ct)
    {
        IAsyncDisposable lockHandle = null;

        try
        {
            lockHandle = await _lock.TryAcquireAsync(JobName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{JobName}: failed to acquire lock", JobName);
            return;
        }

        // Another instance already holds the lock; skip this cycle.
        if (lockHandle == null)
        {
            _logger.LogInformation("{JobName}: skipped (lock held by another instance)", JobName);
            return;
        }

        var startedAt = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N")[..12];
        var sw = Stopwatch.StartNew();

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["JobName"] = JobName,
            ["InstanceId"] = _instanceId
        });

        _logger.LogInformation("{JobName}: started on {InstanceId}", JobName, _instanceId);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var itemsProcessed = await RunJobAsync(scope, ct);
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;

            // Adaptive back-off: double the interval when idle, reset when there is work.
            if (itemsProcessed == 0)
            {
                var doubled = _currentInterval.TotalMilliseconds > 0
                    ? TimeSpan.FromMilliseconds(_currentInterval.TotalMilliseconds * 2)
                    : TimeSpan.FromMilliseconds(Interval.TotalMilliseconds * 2);

                _currentInterval = doubled > EffectiveMaxInterval ? EffectiveMaxInterval : doubled;
            }
            else
            {
                _currentInterval = Interval;
            }

            _logger.LogInformation("{JobName}: completed in {DurationMs}ms, processed {ItemsProcessed} items",
                JobName, durationMs, itemsProcessed);

            await RecordExecutionAsync(JobExecutionStatus.Completed, durationMs, itemsProcessed, null, startedAt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;
            _logger.LogInformation("{JobName}: cancelled after {DurationMs}ms", JobName, durationMs);

            // Use CancellationToken.None so the audit write still completes even though the
            // original token has been cancelled.
            await RecordExecutionAsync(JobExecutionStatus.Cancelled, durationMs, null, null, startedAt, CancellationToken.None);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;
            _logger.LogError(ex, "{JobName}: failed after {DurationMs}ms", JobName, durationMs);

            await RecordExecutionAsync(JobExecutionStatus.Failed, durationMs, null, ex.Message, startedAt, CancellationToken.None);
        }
        finally
        {
            await lockHandle.DisposeAsync();
        }
    }

    // Writes a JobExecution audit row. Errors here are swallowed so a logging failure
    // never disrupts the worker loop — only a warning is emitted.
    private async Task RecordExecutionAsync(string status, int? durationMs, int? itemsProcessed,
        string errorMessage, DateTime startedAt, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<ISessionFactory>();
            using var session = factory.OpenSession();
            using var tx = session.BeginTransaction();

            var execution = new JobExecution
            {
                JobName = JobName,
                InstanceId = _instanceId,
                StartedAt = startedAt,
                CompletedAt = status is JobExecutionStatus.Completed or JobExecutionStatus.Failed or JobExecutionStatus.Cancelled
                    ? DateTime.UtcNow
                    : null,
                DurationMs = durationMs,
                Status = status,
                ItemsProcessed = itemsProcessed,
                // Truncate error messages that exceed the column width.
                ErrorMessage = errorMessage != null && errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage,
                CreatedAt = DateTime.UtcNow
            };

            await session.SaveAsync(execution, ct);
            await session.FlushAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{JobName}: failed to record execution", JobName);
        }
    }
}
