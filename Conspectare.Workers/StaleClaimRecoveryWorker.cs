using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class StaleClaimRecoveryWorker : DistributedBackgroundService
{
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);
    protected override string JobName => "stale_claim_recovery";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(2);
    public StaleClaimRecoveryWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<StaleClaimRecoveryWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<StaleClaimRecoveryWorker>>();
        var cutoff = DateTime.UtcNow - StaleThreshold;
        var recovered = new RecoverStaleDocumentsCommand(cutoff).Execute();
        if (recovered > 0)
        {
            logger.LogWarning(
                "StaleClaimRecovery: recovered {Count} stuck document(s) older than {Threshold}",
                recovered, StaleThreshold);
        }
        return Task.FromResult(recovered);
    }
}
