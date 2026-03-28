using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveRefreshTokenCommand(RefreshToken token) : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists a new refresh token record to the database.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Save(token);
    }
}
