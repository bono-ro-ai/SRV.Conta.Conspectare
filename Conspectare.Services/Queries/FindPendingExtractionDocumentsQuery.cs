using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindPendingExtractionDocumentsQuery(int batchSize)
    : NHibernateConspectareQuery<IList<Document>>
{
    /// <summary>
    /// Returns up to <paramref name="batchSize"/> documents in the <see cref="DocumentStatus.PendingExtraction"/>
    /// state, ordered oldest-first so the extraction worker processes them in arrival order.
    /// </summary>
    protected override IList<Document> OnExecute()
    {
        return Session.QueryOver<Document>()
            .Where(d => d.Status == DocumentStatus.PendingExtraction)
            .OrderBy(d => d.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
