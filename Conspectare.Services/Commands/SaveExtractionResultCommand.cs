using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveExtractionResultCommand(
    Document document,
    CanonicalOutput canonicalOutput,
    ExtractionAttempt attempt,
    DocumentEvent statusEvent,
    DocumentArtifact artifact,
    IList<ReviewFlag> reviewFlags)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Merge(document);

        canonicalOutput.Document = document;
        canonicalOutput.DocumentId = document.Id;
        Session.Save(canonicalOutput);

        if (artifact != null)
        {
            artifact.Document = document;
            artifact.DocumentId = document.Id;
            Session.Save(artifact);

            attempt.ResponseArtifactId = artifact.Id;
        }

        attempt.Document = document;
        attempt.DocumentId = document.Id;
        Session.Save(attempt);

        if (reviewFlags is { Count: > 0 })
        {
            foreach (var flag in reviewFlags)
            {
                flag.Document = document;
                flag.DocumentId = document.Id;
                Session.Save(flag);
            }
        }

        statusEvent.Document = document;
        statusEvent.DocumentId = document.Id;
        Session.Save(statusEvent);
    }
}
