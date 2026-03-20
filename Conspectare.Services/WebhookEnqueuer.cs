using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;

namespace Conspectare.Services;

public static class WebhookEnqueuer
{
    private static readonly HashSet<string> WebhookEligibleStatuses = new()
    {
        DocumentStatus.Completed,
        DocumentStatus.Failed,
        DocumentStatus.Rejected,
        DocumentStatus.ReviewRequired,
    };

    public static void EnqueueIfNeeded(Document doc, ApiClient client, DateTime utcNow)
    {
        if (!WebhookEligibleStatuses.Contains(doc.Status))
            return;

        if (string.IsNullOrWhiteSpace(client?.WebhookUrl))
            return;

        var payloadJson = WebhookPayloadBuilder.Build(doc, utcNow);

        var delivery = new WebhookDelivery
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            WebhookUrl = client.WebhookUrl,
            PayloadJson = payloadJson,
            Status = "pending",
            AttemptCount = 0,
            MaxAttempts = 3,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        new SaveWebhookDeliveryCommand(delivery).Execute();
    }
}
