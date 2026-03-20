using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
using NHibernate;

namespace Conspectare.Services.Queries;

public class LoadReviewDocumentDetailQuery(long tenantId, long documentId)
    : NHibernateConspectareQuery<Document>
{
    protected override Document OnExecute()
    {
        CanonicalOutput canonicalAlias = null;
        ReviewFlag reviewFlagAlias = null;

        return Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.Id == documentId)
            .Left.JoinAlias(d => d.CanonicalOutput, () => canonicalAlias)
            .Left.JoinAlias(d => d.ReviewFlags, () => reviewFlagAlias)
            .SingleOrDefault();
    }
}
