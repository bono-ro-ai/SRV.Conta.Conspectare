using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class UpdateVatFlagMessageCommand(ReviewFlag flag, string newMessage) : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var merged = (ReviewFlag)Session.Merge(flag);
        merged.Message = newMessage;
    }
}
