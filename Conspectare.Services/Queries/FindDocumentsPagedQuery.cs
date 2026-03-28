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
    /// <summary>
    /// Returns a paginated, filtered list of documents. When <paramref name="tenantId"/> is zero the
    /// query is scoped across all tenants (admin view); otherwise it is restricted to the given tenant.
    /// Filters for status, free-text search (external ref or document ref), and date range are applied
    /// via a shared helper to keep the count and list queries consistent.
    /// </summary>
    protected override PagedResult<Document> OnExecute()
    {
        // Build the count query first so we have a total for pagination metadata.
        var countQuery = Session.QueryOver<Document>();
        if (tenantId > 0)
            countQuery = countQuery.Where(d => d.TenantId == tenantId);
        ApplyFilters(countQuery);
        var totalCount = countQuery.RowCount();

        // Build the list query with the same filters, then apply ordering and paging.
        var listQuery = Session.QueryOver<Document>();
        if (tenantId > 0)
            listQuery = listQuery.Where(d => d.TenantId == tenantId);
        ApplyFilters(listQuery);
        var items = listQuery
            .OrderBy(d => d.CreatedAt).Desc
            // Convert 1-based page number to a 0-based row offset.
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .List();

        return new PagedResult<Document>(items.ToList().AsReadOnly(), totalCount, page, pageSize);
    }

    /// <summary>
    /// Applies status, free-text search, and date-range filters to the given query.
    /// Extracted into a helper so both the count and list queries stay in sync.
    /// </summary>
    private void ApplyFilters(NHibernate.IQueryOver<Document, Document> query)
    {
        // Use a simple equality when only one status is supplied; fall back to IN for multiple.
        if (statuses.Count == 1)
            query.And(d => d.Status == statuses[0]);
        else if (statuses.Count > 1)
            query.AndRestrictionOn(d => d.Status).IsIn(statuses.ToArray());

        if (!string.IsNullOrWhiteSpace(search))
            // Case-insensitive substring match across both reference fields.
            query.And(Restrictions.Disjunction()
                .Add(Restrictions.InsensitiveLike(nameof(Document.ExternalRef), search, MatchMode.Anywhere))
                .Add(Restrictions.InsensitiveLike(nameof(Document.DocumentRef), search, MatchMode.Anywhere)));

        if (dateFrom.HasValue)
            query.And(d => d.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query.And(d => d.CreatedAt <= dateTo.Value);
    }
}
