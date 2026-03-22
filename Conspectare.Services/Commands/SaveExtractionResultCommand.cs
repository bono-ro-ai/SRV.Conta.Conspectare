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
        var merged = (Document)Session.Merge(document);

        canonicalOutput.Document = merged;
        canonicalOutput.DocumentId = merged.Id;
        var existingOutput = Session.CreateSQLQuery(
                "SELECT id FROM pipe_canonical_outputs WHERE document_id = :docId")
            .SetParameter("docId", merged.Id)
            .UniqueResult<long?>();
        if (existingOutput.HasValue)
        {
            canonicalOutput.Id = existingOutput.Value;
            Session.Merge(canonicalOutput);
        }
        else
        {
            Session.Save(canonicalOutput);
        }

        if (artifact != null)
        {
            artifact.Document = merged;
            artifact.DocumentId = merged.Id;
            Session.Save(artifact);

            attempt.ResponseArtifactId = artifact.Id;
        }

        attempt.Document = merged;
        attempt.DocumentId = merged.Id;
        Session.Save(attempt);

        if (reviewFlags is { Count: > 0 })
        {
            foreach (var flag in reviewFlags)
            {
                flag.Document = merged;
                flag.DocumentId = merged.Id;
                Session.Save(flag);
            }
        }

        statusEvent.Document = merged;
        statusEvent.DocumentId = merged.Id;
        Session.Save(statusEvent);
    }
}
