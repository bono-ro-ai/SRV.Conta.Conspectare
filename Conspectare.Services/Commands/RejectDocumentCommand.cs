using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RejectDocumentCommand(
    Document document,
    DocumentEvent auditEvent)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Update(document);
        Session.Save(auditEvent);
    }
}
