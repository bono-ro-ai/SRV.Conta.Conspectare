using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Models;

namespace Conspectare.Services.Queries;

public class FindReviewQueueDocumentsQuery(long tenantId, int page, int pageSize)
    : NHibernateConspectareQuery<PagedResult<Document>>
{
    protected override PagedResult<Document> OnExecute()
    {
        var totalCount = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.Status == DocumentStatus.ReviewRequired)
            .RowCount();

        var items = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.Status == DocumentStatus.ReviewRequired)
            .OrderBy(d => d.CreatedAt).Asc
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .List();

        return new PagedResult<Document>(items.ToList().AsReadOnly(), totalCount, page, pageSize);
    }
}
