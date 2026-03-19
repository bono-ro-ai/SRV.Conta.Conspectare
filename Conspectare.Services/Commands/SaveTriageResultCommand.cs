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
        var merged = (Document)Session.Merge(document);

        attempt.Document = merged;
        attempt.DocumentId = merged.Id;
        Session.Save(attempt);

        statusEvent.Document = merged;
        statusEvent.DocumentId = merged.Id;
        Session.Save(statusEvent);
    }
}
