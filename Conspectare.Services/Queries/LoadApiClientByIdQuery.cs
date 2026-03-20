using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadApiClientByIdQuery(long clientId)
    : NHibernateConspectareQuery<ApiClient>
{
    protected override ApiClient OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .Where(c => c.Id == clientId)
            .SingleOrDefault();
    }
}
