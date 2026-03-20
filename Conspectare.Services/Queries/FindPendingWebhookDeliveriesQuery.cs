using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindPendingWebhookDeliveriesQuery(int batchSize)
    : NHibernateConspectareQuery<IList<WebhookDelivery>>
{
    protected override IList<WebhookDelivery> OnExecute()
    {
        var utcNow = DateTime.UtcNow;

        return Session.QueryOver<WebhookDelivery>()
            .Where(w => w.Status == "pending")
            .And(Restrictions.Disjunction()
                .Add(Restrictions.On<WebhookDelivery>(w => w.NextAttemptAt).IsNull)
                .Add(Restrictions.Where<WebhookDelivery>(w => w.NextAttemptAt <= utcNow)))
            .OrderBy(w => w.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
