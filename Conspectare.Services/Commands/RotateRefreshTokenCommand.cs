using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RotateRefreshTokenCommand(long oldTokenId, RefreshToken newToken)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var oldToken = Session.Get<RefreshToken>(oldTokenId);
        oldToken.RevokedAt = DateTime.UtcNow;
        Session.Update(oldToken);
        Session.Save(newToken);
        oldToken.ReplacedByTokenId = newToken.Id;
        Session.Update(oldToken);
    }
}
