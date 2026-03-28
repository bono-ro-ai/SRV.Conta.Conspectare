using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class UpdateWebhookDeliveryCommand(WebhookDelivery delivery)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Merges the updated webhook delivery state (e.g. response code, retry count)
    /// into the session, persisting any changes to the existing record.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Merge(delivery);
    }
}
