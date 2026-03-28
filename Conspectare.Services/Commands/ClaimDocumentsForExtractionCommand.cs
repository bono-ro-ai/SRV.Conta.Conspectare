using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class ClaimDocumentsForExtractionCommand(IList<Document> documents)
    : NHibernateConspectareCommand<IList<Document>>
{
    /// <summary>
    /// Atomically claims each candidate document for extraction using an optimistic
    /// conditional UPDATE that only succeeds if the row is still in
    /// <see cref="DocumentStatus.PendingExtraction"/>. Returns the subset that were
    /// successfully claimed.
    /// </summary>
    protected override IList<Document> OnExecute()
    {
        var claimed = new List<Document>();
        var utcNow = DateTime.UtcNow;

        foreach (var doc in documents)
        {
            // Conditional UPDATE guards against double-claiming when multiple
            // worker instances run concurrently — only the row that still holds
            // PendingExtraction status will be updated (affected rows == 1).
            var updated = Session.CreateSQLQuery(
                    "UPDATE pipe_documents SET status = :newStatus, updated_at = :now " +
                    "WHERE id = :id AND status = :expectedStatus")
                .SetParameter("newStatus", DocumentStatus.Extracting)
                .SetParameter("now", utcNow)
                .SetParameter("id", doc.Id)
                .SetParameter("expectedStatus", DocumentStatus.PendingExtraction)
                .ExecuteUpdate();

            if (updated == 1)
            {
                doc.Status = DocumentStatus.Extracting;
                doc.UpdatedAt = utcNow;
                Session.Refresh(doc);
                claimed.Add(doc);
            }
        }

        return claimed;
    }
}
