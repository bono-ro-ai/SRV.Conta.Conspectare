using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SoftDeleteApiClientCommand(long clientId)
    : NHibernateConspectareCommand<bool>
{
    /// <summary>
    /// Deactivates the API client with the given <paramref name="clientId"/> by
    /// setting <c>IsActive = false</c>. Returns <c>true</c> if the client was found
    /// and updated, or <c>false</c> if no matching client exists.
    /// </summary>
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
