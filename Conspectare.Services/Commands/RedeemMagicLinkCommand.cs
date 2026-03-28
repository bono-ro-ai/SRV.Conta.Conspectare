using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RedeemMagicLinkCommand(MagicLinkToken token, User user, RefreshToken refreshToken)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Marks the magic-link token as used, updates the user's last-login timestamp,
    /// and persists the new refresh token — all in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        token.UsedAt = DateTime.UtcNow;
        Session.Update(token);

        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        Session.Update(user);

        Session.Save(refreshToken);
    }
}
