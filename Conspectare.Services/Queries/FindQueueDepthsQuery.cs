using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindQueueDepthsQuery(long tenantId)
    : NHibernateConspectareQuery<IList<QueueDepthResult>>
{
    protected override IList<QueueDepthResult> OnExecute()
    {
        QueueDepthResult result = null;

        return Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .SelectList(list => list
                .SelectGroup(d => d.Status).WithAlias(() => result.Status)
                .SelectCount(d => d.Id).WithAlias(() => result.Count))
            .TransformUsing(NHibernate.Transform.Transformers.AliasToBean<QueueDepthResult>())
            .List<QueueDepthResult>();
    }
}

public class QueueDepthResult
{
    public string Status { get; set; }
    public int Count { get; set; }
}
