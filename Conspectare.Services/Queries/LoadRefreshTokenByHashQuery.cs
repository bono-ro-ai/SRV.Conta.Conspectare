using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadRefreshTokenByHashQuery(string tokenHash) : NHibernateConspectareQuery<RefreshToken>
{
    protected override RefreshToken OnExecute()
    {
        return Session.QueryOver<RefreshToken>()
            .Where(rt => rt.TokenHash == tokenHash)
            .Fetch(SelectMode.Fetch, rt => rt.User)
            .SingleOrDefault();
    }
}
