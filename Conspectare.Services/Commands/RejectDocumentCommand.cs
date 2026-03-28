using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RejectDocumentCommand(
    Document document,
    DocumentEvent auditEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the rejection of a document: updates the document record and saves
    /// the rejection audit event in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Update(document);
        Session.Save(auditEvent);
    }
}
