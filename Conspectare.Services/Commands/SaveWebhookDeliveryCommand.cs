using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveWebhookDeliveryCommand(WebhookDelivery delivery)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Save(delivery);
    }
}
