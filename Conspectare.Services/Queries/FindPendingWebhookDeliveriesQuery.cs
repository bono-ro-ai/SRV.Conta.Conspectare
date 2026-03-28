using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindPendingWebhookDeliveriesQuery(int batchSize)
    : NHibernateConspectareQuery<IList<WebhookDelivery>>
{
    /// <summary>
    /// Returns up to <paramref name="batchSize"/> pending webhook deliveries that are ready to be
    /// dispatched. A delivery is considered ready when its <c>NextAttemptAt</c> is null (first
    /// attempt, never scheduled for delay) or has passed the current UTC time (retry window elapsed).
    /// Results are ordered oldest-first to ensure fair FIFO delivery across tenants.
    /// </summary>
    protected override IList<WebhookDelivery> OnExecute()
    {
        // Capture utcNow once so all rows in the batch share the same consistent cutoff.
        var utcNow = DateTime.UtcNow;

        return Session.QueryOver<WebhookDelivery>()
            .Where(w => w.Status == WebhookDeliveryStatus.Pending)
            // Include deliveries with no scheduled delay (first attempt) OR whose retry window has elapsed.
            .And(Restrictions.Disjunction()
                .Add(Restrictions.On<WebhookDelivery>(w => w.NextAttemptAt).IsNull)
                .Add(Restrictions.Where<WebhookDelivery>(w => w.NextAttemptAt <= utcNow)))
            .OrderBy(w => w.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
