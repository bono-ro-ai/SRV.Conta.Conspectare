using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ResetDocumentForRetryCommand(Document document, DocumentEvent retryEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Resets a failed or stale document back to <see cref="DocumentStatus.PendingTriage"/>,
    /// increments its retry counter, clears any previous error message, and records
    /// the retry audit event — all in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        document.Status = DocumentStatus.PendingTriage;
        document.RetryCount++;
        document.ErrorMessage = null;
        document.UpdatedAt = DateTime.UtcNow;

        // Merge re-attaches the detached document before persisting updated fields.
        var merged = (Document)Session.Merge(document);

        retryEvent.Document = merged;
        retryEvent.DocumentId = merged.Id;
        Session.Save(retryEvent);
    }
}
