using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class UpdateVatFlagMessageCommand(ReviewFlag flag, string newMessage) : NHibernateConspectareCommand
{
    /// <summary>
    /// Merges the given review flag into the session and updates its human-readable
    /// message in place. Used to enrich a VAT flag after an ANAF validation result
    /// becomes available.
    /// </summary>
    protected override void OnExecute()
    {
        var merged = (ReviewFlag)Session.Merge(flag);
        merged.Message = newMessage;
    }
}
