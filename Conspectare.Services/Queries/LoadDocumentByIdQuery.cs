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
        DocumentEvent eventAlias = null;
        ExtractionAttempt extractionAlias = null;

        return Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.Id == documentId)
            .Left.JoinAlias(d => d.CanonicalOutput, () => canonicalAlias)
            .Left.JoinAlias(d => d.ReviewFlags, () => reviewFlagAlias)
            .Left.JoinAlias(d => d.Events, () => eventAlias)
            .Left.JoinAlias(d => d.ExtractionAttempts, () => extractionAlias)
            .TransformUsing(NHibernate.Transform.Transformers.DistinctRootEntity)
            .SingleOrDefault();
    }
}
