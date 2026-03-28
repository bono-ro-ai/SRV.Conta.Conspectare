using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadApiClientByIdQuery(long clientId)
    : NHibernateConspectareQuery<ApiClient>
{
    /// <summary>
    /// Returns the API client with the specified ID, or null if it does not exist.
    /// </summary>
    protected override ApiClient OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .Where(c => c.Id == clientId)
            .SingleOrDefault();
    }
}
