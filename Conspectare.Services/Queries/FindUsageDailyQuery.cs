using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindUsageDailyQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<IList<UsageDaily>>
{
    protected override IList<UsageDaily> OnExecute()
    {
        return Session.QueryOver<UsageDaily>()
            .Where(u => u.TenantId == tenantId)
            .And(u => u.UsageDate >= from.Date)
            .And(u => u.UsageDate <= to.Date)
            .OrderBy(u => u.UsageDate).Asc
            .List();
    }
}
