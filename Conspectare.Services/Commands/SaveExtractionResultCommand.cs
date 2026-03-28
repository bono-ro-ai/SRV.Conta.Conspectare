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
    /// <summary>
    /// Persists the full outcome of a single-model extraction run: merges the
    /// document state, upserts the canonical output (insert on first run, merge on
    /// subsequent retries), saves the optional raw-response artifact, saves the
    /// extraction attempt, saves any generated review flags, and records the
    /// status-change audit event — all in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        // Merge re-attaches the detached document snapshot returned by the extraction worker.
        var merged = (Document)Session.Merge(document);

        canonicalOutput.Document = merged;
        canonicalOutput.DocumentId = merged.Id;

        // Upsert logic: on a retry a canonical output row may already exist for this
        // document, so we merge instead of inserting a duplicate.
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

        // Artifact is optional — some extraction paths do not produce a raw-response file.
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
