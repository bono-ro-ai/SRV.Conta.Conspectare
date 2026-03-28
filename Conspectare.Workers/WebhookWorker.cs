using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Background worker responsible for dispatching pending outbound webhook deliveries.
/// Processes up to <see cref="BatchSize"/> deliveries per run, permanently failing any
/// delivery whose associated API client can no longer be found.
/// </summary>
public class WebhookWorker : DistributedBackgroundService
{
    private const int BatchSize = 10;

    protected override string JobName => "webhook_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(5);

    /// <summary>Initialises the worker with the required infrastructure dependencies.</summary>
    public WebhookWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<WebhookWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }

    /// <summary>
    /// Loads a batch of pending webhook deliveries and attempts to dispatch each one.
    /// Returns the number of deliveries that were processed (dispatched or permanently failed).
    /// </summary>
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
            ct.ThrowIfCancellationRequested();

            try
            {
                var client = new LoadApiClientByIdQuery(delivery.TenantId).Execute();

                // If the tenant's API client has been deleted, there is no webhook secret to
                // sign with, so the delivery can never succeed — mark it permanently failed.
                if (client == null)
                {
                    delivery.Status = WebhookDeliveryStatus.FailedPermanently;
                    delivery.ErrorMessage = "ApiClient not found";
                    delivery.UpdatedAt = DateTime.UtcNow;
                    new UpdateWebhookDeliveryCommand(delivery).Execute();
                    processedCount++;
                    continue;
                }

                await dispatchService.DispatchAsync(delivery, client.WebhookSecret, ct);
                new UpdateWebhookDeliveryCommand(delivery).Execute();
                processedCount++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
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
