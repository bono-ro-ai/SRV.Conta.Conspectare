using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Models;

namespace Conspectare.Services.Queries;

public class FindDocumentsPagedQuery(long tenantId, string status, int page, int pageSize)
    : NHibernateConspectareQuery<PagedResult<Document>>
{
    protected override PagedResult<Document> OnExecute()
    {
        var countQuery = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
            countQuery = countQuery.And(d => d.Status == status);

        var totalCount = countQuery.RowCount();

        var listQuery = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(status))
            listQuery = listQuery.And(d => d.Status == status);

        var items = listQuery
            .OrderBy(d => d.CreatedAt).Desc
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .List();

        return new PagedResult<Document>(items.ToList().AsReadOnly(), totalCount, page, pageSize);
    }
}
