using System.Diagnostics;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NHibernate;
namespace Conspectare.Workers;
public abstract class DistributedBackgroundService : BackgroundService
{
    private static readonly TimeSpan MaxAdaptiveInterval = TimeSpan.FromSeconds(10);
    private TimeSpan EffectiveMaxInterval => TimeSpan.FromMilliseconds(
        Math.Max(MaxAdaptiveInterval.TotalMilliseconds, Interval.TotalMilliseconds));
    private readonly IDistributedLock _lock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly IPipelineSignal _signal;
    private readonly string _instanceId;
    private TimeSpan _currentInterval;
    protected abstract string JobName { get; }
    protected abstract TimeSpan Interval { get; }
    protected virtual bool IsOneShot => false;
    protected virtual TimeSpan StartupDelay => TimeSpan.FromSeconds(5);
    protected virtual string SignalStage => null;
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
        _instanceId = $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}";
    }
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
    protected virtual TimeSpan ComputeDelay()
    {
        if (_currentInterval == TimeSpan.Zero)
            _currentInterval = Interval;
        return _currentInterval;
    }
    protected abstract Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct);
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
        if (lockHandle == null)
        {
            _logger.LogInformation("{JobName}: skipped (lock held by another instance)", JobName);
            return;
        }
        var startedAt = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("{JobName}: started on {InstanceId}", JobName, _instanceId);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var itemsProcessed = await RunJobAsync(scope, ct);
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;
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
            await RecordExecutionAsync("completed", durationMs, itemsProcessed, null, startedAt, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;
            _logger.LogInformation("{JobName}: cancelled after {DurationMs}ms", JobName, durationMs);
            await RecordExecutionAsync("cancelled", durationMs, null, null, startedAt, CancellationToken.None);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var durationMs = (int)sw.ElapsedMilliseconds;
            _logger.LogError(ex, "{JobName}: failed after {DurationMs}ms", JobName, durationMs);
            await RecordExecutionAsync("failed", durationMs, null, ex.Message, startedAt, CancellationToken.None);
        }
        finally
        {
            await lockHandle.DisposeAsync();
        }
    }
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
                CompletedAt = status is "completed" or "failed" or "cancelled" ? DateTime.UtcNow : null,
                DurationMs = durationMs,
                Status = status,
                ItemsProcessed = itemsProcessed,
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
