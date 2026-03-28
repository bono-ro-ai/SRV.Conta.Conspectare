using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveUserCommand(User user) : NHibernateConspectareCommand
{
    /// <summary>
    /// Inserts or updates the given user record. Uses <c>SaveOrUpdate</c> to handle
    /// both new registrations (no id) and profile updates (existing id).
    /// </summary>
    protected override void OnExecute()
    {
        Session.SaveOrUpdate(user);
    }
}
