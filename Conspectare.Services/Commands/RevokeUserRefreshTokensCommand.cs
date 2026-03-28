using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RevokeUserRefreshTokensCommand(long userId) : NHibernateConspectareCommand
{
    /// <summary>
    /// Revokes all active (non-revoked) refresh tokens belonging to the specified
    /// user by stamping their <c>RevokedAt</c> timestamp. Used during logout or
    /// forced session termination.
    /// </summary>
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
