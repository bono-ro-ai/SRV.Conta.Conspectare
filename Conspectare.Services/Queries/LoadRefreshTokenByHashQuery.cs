using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadRefreshTokenByHashQuery(string tokenHash) : NHibernateConspectareQuery<RefreshToken>
{
    /// <summary>
    /// Loads the refresh token matching the given hash, eagerly fetching its associated user
    /// in the same query to avoid a lazy-load round-trip during token validation.
    /// Returns null if no matching token exists.
    /// </summary>
    protected override RefreshToken OnExecute()
    {
        return Session.QueryOver<RefreshToken>()
            .Where(rt => rt.TokenHash == tokenHash)
            .Fetch(SelectMode.Fetch, rt => rt.User)
            .SingleOrDefault();
    }
}
