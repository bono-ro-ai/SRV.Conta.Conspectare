using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAllActiveTenantsQuery()
    : NHibernateConspectareQuery<IList<ApiClient>>
{
    /// <summary>
    /// Returns all active API clients (tenants). Used by background services that need to
    /// iterate over every live tenant, e.g. to schedule usage snapshots or health checks.
    /// </summary>
    protected override IList<ApiClient> OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .Where(c => c.IsActive == true)
            .List();
    }
}
