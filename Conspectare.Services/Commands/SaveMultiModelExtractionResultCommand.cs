using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveMultiModelExtractionResultCommand(
    Document document,
    CanonicalOutput canonicalOutput,
    IList<ExtractionAttempt> attempts,
    DocumentEvent statusEvent,
    IList<DocumentArtifact> artifacts,
    IList<ReviewFlag> reviewFlags)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the outcome of a multi-model extraction run: merges the document
    /// state, saves the canonical output, saves all raw-response artifacts, links
    /// each extraction attempt to its corresponding artifact by positional index,
    /// saves any generated review flags, and records the status-change audit event —
    /// all in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        // Merge re-attaches the detached document snapshot returned by the extraction worker.
        var merged = (Document)Session.Merge(document);

        canonicalOutput.Document = merged;
        canonicalOutput.DocumentId = merged.Id;
        Session.Save(canonicalOutput);

        foreach (var artifact in artifacts)
        {
            artifact.Document = merged;
            artifact.DocumentId = merged.Id;
            Session.Save(artifact);
        }

        // Each attempt is paired with the artifact at the same list position.
        // If there are fewer artifacts than attempts, trailing attempts have no artifact.
        for (var i = 0; i < attempts.Count; i++)
        {
            var attempt = attempts[i];
            attempt.Document = merged;
            attempt.DocumentId = merged.Id;

            if (i < artifacts.Count)
                attempt.ResponseArtifactId = artifacts[i].Id;

            Session.Save(attempt);
        }

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
