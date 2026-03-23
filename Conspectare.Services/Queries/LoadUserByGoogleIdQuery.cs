using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByGoogleIdQuery(string googleId) : NHibernateConspectareQuery<User>
{
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.GoogleId == googleId)
            .SingleOrDefault();
    }
}
