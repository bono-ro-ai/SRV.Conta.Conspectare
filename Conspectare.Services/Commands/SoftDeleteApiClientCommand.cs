using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SoftDeleteApiClientCommand(long clientId)
    : NHibernateConspectareCommand<bool>
{
    protected override bool OnExecute()
    {
        var client = Session.QueryOver<ApiClient>()
            .Where(c => c.Id == clientId)
            .SingleOrDefault();
        if (client == null)
            return false;
        client.IsActive = false;
        client.UpdatedAt = DateTime.UtcNow;
        Session.Update(client);
        return true;
    }
}
