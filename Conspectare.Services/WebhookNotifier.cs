using Conspectare.Domain.Entities;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Logging;
namespace Conspectare.Services;
public static class WebhookNotifier
{
    public static void NotifyIfNeeded(Document doc, ILogger logger,
        CanonicalOutput canonicalOutput = null, IList<ReviewFlag> reviewFlags = null)
    {
        try
        {
            var client = new LoadApiClientByIdQuery(doc.TenantId).Execute();
            WebhookEnqueuer.EnqueueIfNeeded(doc, client, DateTime.UtcNow, canonicalOutput, reviewFlags);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "WebhookNotifier: failed to enqueue webhook for document {DocumentId}", doc.Id);
        }
    }
}
