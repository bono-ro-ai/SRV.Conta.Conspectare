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
    protected override void OnExecute()
    {
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
