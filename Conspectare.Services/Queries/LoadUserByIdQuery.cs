using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByIdQuery(long userId) : NHibernateConspectareQuery<User>
{
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.Id == userId)
            .SingleOrDefault();
    }
}
