using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class WebhookWorker : DistributedBackgroundService
{
    private const int BatchSize = 10;
    protected override string JobName => "webhook_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(5);
    public WebhookWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WebhookWorker>>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IWebhookDispatchService>();
        var pendingDeliveries = new FindPendingWebhookDeliveriesQuery(BatchSize).Execute();
        if (pendingDeliveries.Count == 0)
            return 0;
        var processedCount = 0;
        foreach (var delivery in pendingDeliveries)
        {
            try
            {
                await dispatchService.DispatchAsync(delivery, ct);
                new UpdateWebhookDeliveryCommand(delivery).Execute();
                processedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "WebhookWorker: failed to dispatch webhook for document {DocumentId}",
                    delivery.DocumentId);
            }
        }
        return processedCount;
    }
}
