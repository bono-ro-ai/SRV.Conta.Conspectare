using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class LoadUserByGoogleIdQuery(string googleId) : NHibernateConspectareQuery<User>
{
    /// <summary>
    /// Returns the user associated with the given Google subject identifier,
    /// or null if no user has linked that Google account.
    /// Used during OAuth sign-in to find an existing account before creating a new one.
    /// </summary>
    protected override User OnExecute()
    {
        return Session.QueryOver<User>()
            .Where(u => u.GoogleId == googleId)
            .SingleOrDefault();
    }
}
