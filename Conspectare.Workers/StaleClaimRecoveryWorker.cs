using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
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
public class RecoverStaleDocumentsCommand(DateTime cutoff) : NHibernateConspectareCommand<int>
{
    protected override int OnExecute()
    {
        var triagingCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff")
            .SetParameter("newStatus", DocumentStatus.PendingTriage)
            .SetParameter("staleStatus", DocumentStatus.Triaging)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();
        var extractingCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff")
            .SetParameter("newStatus", DocumentStatus.PendingExtraction)
            .SetParameter("staleStatus", DocumentStatus.Extracting)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();
        return triagingCount + extractingCount;
    }
}
