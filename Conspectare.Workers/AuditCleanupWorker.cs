using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Periodically deletes old job execution audit rows to keep the
/// <c>audit_job_executions</c> table from growing unboundedly.
/// Rows older than 30 days are removed in batches of up to 10 000 per run.
/// </summary>
public class AuditCleanupWorker : DistributedBackgroundService
{
    protected override string JobName => "audit_cleanup_worker";
    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    /// <summary>Initialises the worker with the required infrastructure dependencies.</summary>
    public AuditCleanupWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditCleanupWorker> logger)
        : base(distributedLock, scopeFactory, logger) { }

    /// <summary>
    /// Deletes audit rows that are older than 30 days.
    /// Returns the number of rows deleted.
    /// </summary>
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        using var session = NHibernateConspectare.OpenSession();
        using var tx = session.BeginTransaction();

        var cutoff = DateTime.UtcNow.AddDays(-30);

        // LIMIT 10000 ensures the DELETE does not hold a table lock for too long on busy
        // instances; the next scheduled run will continue where this one left off.
        var deleted = session.CreateSQLQuery(
                "DELETE FROM audit_job_executions WHERE started_at < :cutoff LIMIT 10000")
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();

        tx.Commit();

        return Task.FromResult(deleted);
    }
}
