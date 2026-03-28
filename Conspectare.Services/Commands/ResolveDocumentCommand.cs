using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ResolveDocumentCommand(
    Document document,
    string action,
    string canonicalOutputJson,
    string outputJsonS3Key,
    DocumentEvent resolvedEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Resolves a document under human review by applying the reviewer's chosen
    /// action. If the action is <c>"provide_corrected"</c> the canonical output JSON
    /// is replaced with the corrected version. All open review flags are closed,
    /// the document status is set to <c>Completed</c> or <c>Rejected</c> depending
    /// on the action, and the resolution audit event is saved — all in one transaction.
    /// </summary>
    protected override void OnExecute()
    {
        var utcNow = DateTime.UtcNow;

        // Overwrite canonical output only when the reviewer supplied a correction.
        if (action == "provide_corrected" && document.CanonicalOutput != null)
        {
            document.CanonicalOutput.OutputJson = canonicalOutputJson;
            document.CanonicalOutput.OutputJsonS3Key = outputJsonS3Key;
            Session.Merge(document.CanonicalOutput);
        }

        // Close all open review flags regardless of resolution action.
        foreach (var flag in document.ReviewFlags)
        {
            flag.IsResolved = true;
            flag.ResolvedAt = utcNow;
            Session.Merge(flag);
        }

        var targetStatus = action == "reject"
            ? DocumentStatus.Rejected
            : DocumentStatus.Completed;

        document.Status = targetStatus;
        document.UpdatedAt = utcNow;

        if (targetStatus == DocumentStatus.Completed)
            document.CompletedAt = utcNow;

        var merged = (Document)Session.Merge(document);

        resolvedEvent.Document = merged;
        resolvedEvent.DocumentId = merged.Id;
        Session.Save(resolvedEvent);
    }
}
