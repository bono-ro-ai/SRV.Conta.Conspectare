using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveRefreshTokenCommand(RefreshToken token) : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Save(token);
    }
}
