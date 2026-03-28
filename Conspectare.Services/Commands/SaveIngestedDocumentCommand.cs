using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveIngestedDocumentCommand(
    Document document,
    DocumentArtifact artifact,
    DocumentEvent ingestedEvent,
    DocumentEvent triageEvent)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the newly ingested document together with its initial artifact and the
    /// two audit events (ingested + pending-triage) in a single transaction.
    /// </summary>
    protected override void OnExecute()
    {
        Session.Save(document);

        artifact.Document = document;
        artifact.DocumentId = document.Id;
        Session.Save(artifact);

        ingestedEvent.Document = document;
        ingestedEvent.DocumentId = document.Id;
        Session.Save(ingestedEvent);

        triageEvent.Document = document;
        triageEvent.DocumentId = document.Id;
        Session.Save(triageEvent);
    }
}
