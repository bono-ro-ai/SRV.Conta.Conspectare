using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadDocumentByIdQuery(long tenantId, long documentId)
    : NHibernateConspectareQuery<Document>
{
    protected override Document OnExecute()
    {
        CanonicalOutput canonicalAlias = null;
        ReviewFlag reviewFlagAlias = null;

        var query = Session.QueryOver<Document>();
        if (tenantId > 0)
            query = query.Where(d => d.TenantId == tenantId);
        var document = query
            .And(d => d.Id == documentId)
            .Left.JoinAlias(d => d.CanonicalOutput, () => canonicalAlias)
            .Left.JoinAlias(d => d.ReviewFlags, () => reviewFlagAlias)
            .TransformUsing(NHibernate.Transform.Transformers.DistinctRootEntity)
            .SingleOrDefault();

        if (document != null)
        {
            NHibernateUtil.Initialize(document.Events);
            NHibernateUtil.Initialize(document.ExtractionAttempts);
        }

        return document;
    }
}
