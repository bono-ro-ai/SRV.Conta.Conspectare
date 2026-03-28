using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindTenantVolumesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<IList<VolumeResult>>
{
    /// <summary>
    /// Returns daily document ingestion counts for the specified tenant within the given date range.
    /// The grouping is performed in-process after fetching raw timestamps from the database,
    /// because NHibernate QueryOver does not support a portable DATE() group-by projection.
    /// </summary>
    protected override IList<VolumeResult> OnExecute()
    {
        // Fetch only the CreatedAt timestamps to minimise data transfer before in-process grouping.
        var documents = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CreatedAt >= from)
            .And(d => d.CreatedAt <= to)
            .Select(d => d.CreatedAt)
            .List<DateTime>();

        // Strip the time component and group by calendar date, then sort chronologically.
        return documents
            .GroupBy(dt => dt.Date)
            .Select(g => new VolumeResult { Date = g.Key, Count = g.Count() })
            .OrderBy(v => v.Date)
            .ToList();
    }
}

public class VolumeResult
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}
