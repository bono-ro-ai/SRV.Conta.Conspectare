using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadMagicLinkByHashQuery(string tokenHash) : NHibernateConspectareQuery<MagicLinkToken>
{
    /// <summary>
    /// Loads the magic-link token matching the given hash, eagerly fetching its associated user
    /// in the same query to avoid a lazy-load round-trip during link validation.
    /// Returns null if no matching token exists.
    /// </summary>
    protected override MagicLinkToken OnExecute()
    {
        return Session.QueryOver<MagicLinkToken>()
            .Where(t => t.TokenHash == tokenHash)
            .Fetch(SelectMode.Fetch, t => t.User)
            .SingleOrDefault();
    }
}
