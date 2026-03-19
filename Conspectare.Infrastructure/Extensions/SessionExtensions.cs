using ISession = NHibernate.ISession;

namespace Conspectare.Infrastructure.Extensions;

public static class SessionExtensions
{
    public static void EnableTenantFilter(this ISession session, long tenantId)
    {
        session.EnableFilter("tenantFilter").SetParameter("tenantId", tenantId);
    }
}
