using NHibernate;
using NHibernate.Criterion.Lambda;
using NHibernate.Transform;

namespace Conspectare.Infrastructure.NHibernate.Queries;

public static class QueryOverExtensions
{
    public static IList<TRes> ListAs<TRes>(this IQueryOver qry, TRes resultByExample)
    {
        var ctor = typeof(TRes).GetConstructors().First();
        return qry.UnderlyingCriteria
            .SetResultTransformer(Transformers.AliasToBeanConstructor(ctor))
            .List<TRes>();
    }

    public static IList<TRes> ListAs<TRes>(this IQueryOver qry)
    {
        return qry.UnderlyingCriteria
            .SetResultTransformer(Transformers.AliasToBean<TRes>())
            .List<TRes>();
    }

    public static IQueryOver<TRoot, TSubType> SortAsc<TRoot, TSubType>(
        this IQueryOverOrderBuilder<TRoot, TSubType> queryOverOrderBuilder, bool sortAsc)
    {
        return sortAsc ? queryOverOrderBuilder.Asc : queryOverOrderBuilder.Desc;
    }

    public static List<TRoot> ExecuteList<TRoot>(this IQueryOver<TRoot, TRoot> queryOver) => queryOver.List().ToList();
}
