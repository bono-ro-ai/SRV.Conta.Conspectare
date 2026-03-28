using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindDocumentByExternalRefQuery(long tenantId, string externalRef)
    : NHibernateConspectareQuery<Document>
{
    /// <summary>
    /// Returns the document matching the given external reference within the specified tenant,
    /// or null if no such document exists.
    /// </summary>
    protected override Document OnExecute()
    {
        return Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.ExternalRef == externalRef)
            .SingleOrDefault();
    }
}
