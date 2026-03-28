using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindFailedVatFlagsQuery(int batchSize) : NHibernateConspectareQuery<IList<ReviewFlag>>
{
    /// <summary>
    /// Returns up to <paramref name="batchSize"/> unresolved VAT-validation flags whose message
    /// indicates an external API failure, ordered oldest-first for retry processing.
    /// Only flags of type <c>invalid_supplier_cui</c> or <c>invalid_customer_cui</c> that contain
    /// the literal string "API" in their message are included.
    /// </summary>
    protected override IList<ReviewFlag> OnExecute()
    {
        return Session.QueryOver<ReviewFlag>()
            .Where(f => f.IsResolved == false)
            .And(f => f.FlagType == "invalid_supplier_cui" || f.FlagType == "invalid_customer_cui")
            // Only re-attempt flags whose failure message references a downstream API call,
            // which distinguishes transient API errors from permanent validation failures.
            .And(Restrictions.Like("Message", "%API%"))
            .OrderBy(f => f.CreatedAt).Asc
            .Take(batchSize)
            .List();
    }
}
