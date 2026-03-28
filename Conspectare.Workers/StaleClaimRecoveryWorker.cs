using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Periodically scans for documents that were claimed by a worker but never completed
/// (e.g. due to a crash or restart) and resets them so they can be reprocessed.
/// A document is considered stale when it has been in a claimed/in-progress state for
/// longer than <see cref="StaleThreshold"/>.
/// </summary>
public class StaleClaimRecoveryWorker : DistributedBackgroundService
{
    /// <summary>How long a document must be stuck in a claimed state before it is recovered.</summary>
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(5);

    protected override string JobName => "stale_claim_recovery";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

    /// <summary>Initialises the worker with the required infrastructure dependencies.</summary>
    public StaleClaimRecoveryWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<StaleClaimRecoveryWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }

    /// <summary>
    /// Resets all documents that have been in a stale claimed state for longer than
    /// <see cref="StaleThreshold"/>. Returns the number of documents recovered.
    /// </summary>
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<StaleClaimRecoveryWorker>>();

        // Any document claimed before this point is considered stuck.
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
