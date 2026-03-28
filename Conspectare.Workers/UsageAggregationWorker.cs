using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Cross-tenant worker that aggregates daily usage statistics for all active tenants.
/// Runs once per hour and computes the previous calendar day's usage, upserting a single
/// summary row per tenant so that historical data can be reported without scanning raw events.
/// </summary>
public class UsageAggregationWorker : DistributedBackgroundService
{
    protected override string JobName => "usage_aggregation";
    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    /// <summary>Initialises the worker with the required infrastructure dependencies.</summary>
    public UsageAggregationWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<UsageAggregationWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }

    /// <summary>
    /// Aggregates yesterday's usage for every active tenant and persists the results.
    /// Failures for individual tenants are logged and skipped so that one bad tenant
    /// does not prevent the rest from being aggregated.
    /// Returns the number of tenants successfully aggregated.
    /// </summary>
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UsageAggregationWorker>>();

        // Always aggregate the previous completed calendar day.
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var tenants = new FindAllActiveTenantsQuery().Execute();
        var aggregated = 0;

        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested)
                break;

            try
            {
                var aggregate = new AggregateUsageForTenantQuery(tenant.Id, yesterday).Execute();
                new UpsertUsageDailyCommand(tenant.Id, yesterday, aggregate).Execute();
                aggregated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "UsageAggregation: failed for tenant {TenantId} on {Date}", tenant.Id, yesterday);
            }
        }

        logger.LogInformation(
            "UsageAggregation: aggregated {Date} for {Count} tenant(s)", yesterday, aggregated);

        return Task.FromResult(aggregated);
    }
}
