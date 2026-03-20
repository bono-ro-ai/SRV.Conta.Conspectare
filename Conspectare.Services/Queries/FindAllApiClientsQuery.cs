using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAllApiClientsQuery()
    : NHibernateConspectareQuery<IList<ApiClient>>
{
    protected override IList<ApiClient> OnExecute()
    {
        return Session.QueryOver<ApiClient>()
            .OrderBy(c => c.CreatedAt).Desc
            .List();
    }
}
