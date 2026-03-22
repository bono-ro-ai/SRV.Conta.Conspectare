using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

public class AuditCleanupWorker : DistributedBackgroundService
{
    protected override string JobName => "audit_cleanup_worker";
    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    public AuditCleanupWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditCleanupWorker> logger)
        : base(distributedLock, scopeFactory, logger) { }

    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        using var session = NHibernateConspectare.OpenSession();
        using var tx = session.BeginTransaction();
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var deleted = session.CreateSQLQuery(
                "DELETE FROM audit_job_executions WHERE started_at < :cutoff LIMIT 10000")
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();
        tx.Commit();
        return Task.FromResult(deleted);
    }
}
