using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ResetDocumentForRetryCommand(Document document, DocumentEvent retryEvent)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        document.Status = DocumentStatus.PendingTriage;
        document.RetryCount++;
        document.ErrorMessage = null;
        document.UpdatedAt = DateTime.UtcNow;
        Session.Update(document);

        retryEvent.Document = document;
        retryEvent.DocumentId = document.Id;
        Session.Save(retryEvent);
    }
}
