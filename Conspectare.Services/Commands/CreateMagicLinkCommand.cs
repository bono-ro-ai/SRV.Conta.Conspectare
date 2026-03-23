using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class CreateMagicLinkCommand(User user, MagicLinkToken magicLinkToken, bool isNewUser)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        if (isNewUser)
        {
            Session.Save(user);
            Session.Flush();
        }
        magicLinkToken.UserId = user.Id;
        magicLinkToken.User = user;
        Session.Save(magicLinkToken);
    }
}
