using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RotateRefreshTokenCommand(long oldTokenId, RefreshToken newToken)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Implements refresh-token rotation: revokes the old token, saves the new
    /// replacement token (to obtain its database id), then back-fills the
    /// <c>ReplacedByTokenId</c> link on the old token for audit trail purposes.
    /// The two-step update of the old token is intentional — the new token's id is
    /// only known after the first <c>Save</c> call.
    /// </summary>
    protected override void OnExecute()
    {
        var oldToken = Session.Get<RefreshToken>(oldTokenId);

        oldToken.RevokedAt = DateTime.UtcNow;
        Session.Update(oldToken);

        // Save new token first so its auto-generated id is available.
        Session.Save(newToken);

        // Now link the old token to its replacement for the token-chain audit trail.
        oldToken.ReplacedByTokenId = newToken.Id;
        Session.Update(oldToken);
    }
}
