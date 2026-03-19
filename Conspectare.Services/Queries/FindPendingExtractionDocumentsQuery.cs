using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindPendingExtractionDocumentsQuery(int batchSize)
    : NHibernateConspectareQuery<IList<Document>>
{
    protected override IList<Document> OnExecute()
    {
        return Session.QueryOver<Document>()
            .Where(d => d.Status == DocumentStatus.PendingExtraction)
            .OrderBy(d => d.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
