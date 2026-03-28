using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindQueueDepthsQuery(long tenantId)
    : NHibernateConspectareQuery<IList<QueueDepthResult>>
{
    /// <summary>
    /// Returns the document count grouped by status for the specified tenant,
    /// giving a snapshot of how many documents are in each processing stage.
    /// </summary>
    protected override IList<QueueDepthResult> OnExecute()
    {
        // NHibernate alias trick: a null local is used purely to capture property names for projection aliases.
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
