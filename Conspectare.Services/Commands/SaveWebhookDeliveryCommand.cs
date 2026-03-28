using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveWebhookDeliveryCommand(WebhookDelivery delivery)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists a new webhook delivery record to the database.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Save(delivery);
    }
}
