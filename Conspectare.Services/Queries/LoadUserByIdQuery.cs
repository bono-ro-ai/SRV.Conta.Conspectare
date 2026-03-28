using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByIdQuery(long userId) : NHibernateConspectareQuery<User>
{
    /// <summary>
    /// Returns the user with the specified primary key, or null if no such user exists.
    /// </summary>
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.Id == userId)
            .SingleOrDefault();
    }
}
