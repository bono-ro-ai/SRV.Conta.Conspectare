using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByEmailQuery(string email) : NHibernateConspectareQuery<User>
{
    /// <summary>
    /// Returns the user with the specified email address, or null if no such user exists.
    /// Used during credential-based login and email-verification flows.
    /// </summary>
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.Email == email)
            .SingleOrDefault();
    }
}
