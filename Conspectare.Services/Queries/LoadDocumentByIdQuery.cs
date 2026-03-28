using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadDocumentByIdQuery(long tenantId, long documentId)
    : NHibernateConspectareQuery<Document>
{
    /// <summary>
    /// Loads a document by its primary key, along with its canonical output and review flags
    /// via left outer joins. When <paramref name="tenantId"/> is zero the lookup is cross-tenant
    /// (admin context); otherwise it is scoped to the specified tenant.
    /// After the main fetch, the <c>Events</c> and <c>ExtractionAttempts</c> collections are
    /// explicitly initialized to avoid deferred lazy-loading outside the session.
    /// </summary>
    protected override Document OnExecute()
    {
        CanonicalOutput canonicalAlias = null;
        ReviewFlag reviewFlagAlias = null;

        var query = Session.QueryOver<Document>();
        if (tenantId > 0)
            query = query.Where(d => d.TenantId == tenantId);

        // Left joins on CanonicalOutput and ReviewFlags so the document is always returned even
        // when those child collections are empty. DistinctRootEntity prevents duplicate root
        // objects that would otherwise arise from the join cartesian product.
        var document = query
            .And(d => d.Id == documentId)
            .Left.JoinAlias(d => d.CanonicalOutput, () => canonicalAlias)
            .Left.JoinAlias(d => d.ReviewFlags, () => reviewFlagAlias)
            .TransformUsing(NHibernate.Transform.Transformers.DistinctRootEntity)
            .SingleOrDefault();

        if (document != null)
        {
            // Force-initialize lazy collections while the session is still open,
            // so callers can safely access them after the query completes.
            NHibernateUtil.Initialize(document.Events);
            NHibernateUtil.Initialize(document.ExtractionAttempts);
        }

        return document;
    }
}
