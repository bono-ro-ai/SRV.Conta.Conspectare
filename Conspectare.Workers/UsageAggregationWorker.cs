using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
// Cross-tenant: aggregates usage across all active tenants
public class UsageAggregationWorker : DistributedBackgroundService
{
    protected override string JobName => "usage_aggregation";
    protected override TimeSpan Interval => TimeSpan.FromHours(1);
    public UsageAggregationWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<UsageAggregationWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UsageAggregationWorker>>();
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var tenants = new FindAllActiveTenantsQuery().Execute();
        var aggregated = 0;
        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var aggregate = new AggregateUsageForTenantQuery(tenant.Id, yesterday).Execute();
                new UpsertUsageDailyCommand(tenant.Id, yesterday, aggregate).Execute();
                aggregated++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "UsageAggregation: failed for tenant {TenantId} on {Date}", tenant.Id, yesterday);
            }
        }
        logger.LogInformation("UsageAggregation: aggregated {Date} for {Count} tenant(s)", yesterday, aggregated);
        return Task.FromResult(aggregated);
    }
}
