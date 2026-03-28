using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class CreateMagicLinkCommand(User user, MagicLinkToken magicLinkToken, bool isNewUser)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Creates a magic-link login token for the given user. If the user is new, the
    /// user record is inserted first and the session is flushed so the generated id
    /// is available before the token foreign key is set.
    /// </summary>
    protected override void OnExecute()
    {
        if (isNewUser)
        {
            Session.Save(user);
            // Flush is required here so NHibernate assigns the auto-generated user id
            // before we copy it onto the magic-link token FK below.
            Session.Flush();
        }

        magicLinkToken.UserId = user.Id;
        magicLinkToken.User = user;
        Session.Save(magicLinkToken);
    }
}
