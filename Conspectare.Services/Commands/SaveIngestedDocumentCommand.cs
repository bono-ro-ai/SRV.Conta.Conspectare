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
