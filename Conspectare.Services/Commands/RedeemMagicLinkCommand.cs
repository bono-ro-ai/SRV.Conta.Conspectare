using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RedeemMagicLinkCommand(MagicLinkToken token, User user, RefreshToken refreshToken)
    : NHibernateConspectareCommand
{
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
