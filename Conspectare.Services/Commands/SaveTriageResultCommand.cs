using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveTriageResultCommand(
    Document document,
    ExtractionAttempt attempt,
    DocumentEvent statusEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the triage outcome by merging the updated document state and saving
    /// the extraction attempt record and the status-change audit event.
    /// </summary>
    protected override void OnExecute()
    {
        // Merge re-attaches the detached document snapshot returned by the triage worker.
        var merged = (Document)Session.Merge(document);

        attempt.Document = merged;
        attempt.DocumentId = merged.Id;
        Session.Save(attempt);

        statusEvent.Document = merged;
        statusEvent.DocumentId = merged.Id;
        Session.Save(statusEvent);
    }
}
