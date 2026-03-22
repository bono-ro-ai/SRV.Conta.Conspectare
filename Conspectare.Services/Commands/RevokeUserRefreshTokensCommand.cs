using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RevokeUserRefreshTokensCommand(long userId) : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var now = DateTime.UtcNow;
        var activeTokens = Session.QueryOver<RefreshToken>()
            .Where(rt => rt.UserId == userId)
            .And(rt => rt.RevokedAt == null)
            .List();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            Session.Update(token);
        }
    }
}
