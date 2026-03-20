using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class UpdateWebhookDeliveryCommand(WebhookDelivery delivery)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Merge(delivery);
    }
}
