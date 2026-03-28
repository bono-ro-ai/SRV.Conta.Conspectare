using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ApproveDocumentCommand(
    Document document,
    IList<ReviewFlag> flagsToResolve,
    DocumentEvent auditEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the approval of a document: updates the document record, marks all
    /// outstanding review flags as resolved, and saves the approval audit event —
    /// all within a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Update(document);

        foreach (var flag in flagsToResolve)
            Session.Update(flag);

        Session.Save(auditEvent);
    }
}
