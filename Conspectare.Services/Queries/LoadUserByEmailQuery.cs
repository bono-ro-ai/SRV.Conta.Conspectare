using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByEmailQuery(string email) : NHibernateConspectareQuery<User>
{
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.Email == email)
            .SingleOrDefault();
    }
}
