using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Models;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindDocumentsPagedQuery(
    long tenantId,
    IReadOnlyList<string> statuses,
    string search,
    DateTime? dateFrom,
    DateTime? dateTo,
    int page,
    int pageSize)
    : NHibernateConspectareQuery<PagedResult<Document>>
{
    protected override PagedResult<Document> OnExecute()
    {
        var countQuery = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId);
        ApplyFilters(countQuery);
        var totalCount = countQuery.RowCount();

        var listQuery = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId);
        ApplyFilters(listQuery);
        var items = listQuery
            .OrderBy(d => d.CreatedAt).Desc
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .List();

        return new PagedResult<Document>(items.ToList().AsReadOnly(), totalCount, page, pageSize);
    }

    private void ApplyFilters(NHibernate.IQueryOver<Document, Document> query)
    {
        if (statuses.Count == 1)
            query.And(d => d.Status == statuses[0]);
        else if (statuses.Count > 1)
            query.AndRestrictionOn(d => d.Status).IsIn(statuses.ToArray());

        if (!string.IsNullOrWhiteSpace(search))
            query.And(Restrictions.InsensitiveLike(nameof(Document.ExternalRef), search, MatchMode.Anywhere));

        if (dateFrom.HasValue)
            query.And(d => d.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query.And(d => d.CreatedAt <= dateTo.Value);
    }
}
