using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Filters;

public class TenantFilterDefinition : FilterDefinition
{
    public TenantFilterDefinition()
    {
        WithName("tenantFilter")
            .WithCondition("tenant_id = :tenantId")
            .AddParameter("tenantId", global::NHibernate.NHibernateUtil.Int64);
    }
}
