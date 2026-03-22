using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAllActiveTenantsQuery()
    : NHibernateConspectareQuery<IList<ApiClient>>
{
    protected override IList<ApiClient> OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .Where(c => c.IsActive == true)
            .List();
    }
}
