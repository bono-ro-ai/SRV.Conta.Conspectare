using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAllApiClientsQuery()
    : NHibernateConspectareQuery<IList<ApiClient>>
{
    /// <summary>
    /// Returns all API clients across all tenants, ordered by creation date descending
    /// so the most recently registered clients appear first.
    /// </summary>
    protected override IList<ApiClient> OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .OrderBy(c => c.CreatedAt).Desc
            .List();
    }
}
