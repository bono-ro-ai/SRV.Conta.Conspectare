using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveUserCommand(User user) : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.SaveOrUpdate(user);
    }
}
