using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAnyUserExistsQuery : NHibernateConspectareQuery<bool>
{
    protected override bool OnExecute()
    {
        return Session.QueryOver<User>()
            .RowCount() > 0;
    }
}
