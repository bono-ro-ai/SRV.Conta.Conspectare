using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadMagicLinkByHashQuery(string tokenHash) : NHibernateConspectareQuery<MagicLinkToken>
{
    protected override MagicLinkToken OnExecute()
    {
        return Session.QueryOver<MagicLinkToken>()
            .Where(t => t.TokenHash == tokenHash)
            .Fetch(SelectMode.Fetch, t => t.User)
            .SingleOrDefault();
    }
}
