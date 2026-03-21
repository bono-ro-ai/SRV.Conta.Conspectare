using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindFailedVatFlagsQuery(int batchSize) : NHibernateConspectareQuery<IList<ReviewFlag>>
{
    protected override IList<ReviewFlag> OnExecute()
    {
        return Session.QueryOver<ReviewFlag>()
            .Where(f => f.IsResolved == false)
            .And(f => f.FlagType == "invalid_supplier_cui" || f.FlagType == "invalid_customer_cui")
            .And(Restrictions.Like("Message", "%API%"))
            .OrderBy(f => f.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
