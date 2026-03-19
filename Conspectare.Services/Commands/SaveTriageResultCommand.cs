using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveTriageResultCommand(
    Document document,
    ExtractionAttempt attempt,
    DocumentEvent statusEvent)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        Session.Update(document);

        attempt.Document = document;
        attempt.DocumentId = document.Id;
        Session.Save(attempt);

        statusEvent.Document = document;
        statusEvent.DocumentId = document.Id;
        Session.Save(statusEvent);
    }
}
