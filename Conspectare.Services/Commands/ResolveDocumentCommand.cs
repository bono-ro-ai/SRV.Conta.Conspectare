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
    protected override void OnExecute()
    {
        var utcNow = DateTime.UtcNow;

        if (action == "provide_corrected" && document.CanonicalOutput != null)
        {
            document.CanonicalOutput.OutputJson = canonicalOutputJson;
            document.CanonicalOutput.OutputJsonS3Key = outputJsonS3Key;
            Session.Merge(document.CanonicalOutput);
        }

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
