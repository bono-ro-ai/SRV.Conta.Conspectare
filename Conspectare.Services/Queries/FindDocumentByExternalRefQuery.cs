using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindDocumentByExternalRefQuery(long tenantId, string externalRef)
    : NHibernateConspectareQuery<Document>
{
    protected override Document OnExecute()
    {
        return Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.ExternalRef == externalRef)
            .SingleOrDefault();
    }
}
