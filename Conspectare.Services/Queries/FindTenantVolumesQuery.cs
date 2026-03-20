using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindTenantVolumesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<IList<VolumeResult>>
{
    protected override IList<VolumeResult> OnExecute()
    {
        var documents = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CreatedAt >= from)
            .And(d => d.CreatedAt <= to)
            .Select(d => d.CreatedAt)
            .List<DateTime>();

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
