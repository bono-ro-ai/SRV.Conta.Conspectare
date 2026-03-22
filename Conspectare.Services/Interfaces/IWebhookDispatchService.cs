using Conspectare.Domain.Entities;

namespace Conspectare.Services.Interfaces;

public interface IWebhookDispatchService
{
    Task DispatchAsync(WebhookDelivery delivery, string webhookSecret, CancellationToken ct);
}
