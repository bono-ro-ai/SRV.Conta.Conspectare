using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ApproveDocumentCommand(
    Document document,
    IList<ReviewFlag> flagsToResolve,
    DocumentEvent auditEvent)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Update(document);
        foreach (var flag in flagsToResolve)
            Session.Update(flag);
        Session.Save(auditEvent);
    }
}
