using Conspectare.Infrastructure.NHibernate.Helpers;
using NHibernate;

namespace Conspectare.Services.Core.Database;

public class NHibernateConspectare
{
    private static INHibernateHelper _internalHelper;

    public static void Configure<TMapping>(string connectionString)
    {
        _internalHelper = new MySqlNHibernateHelper().Configure<TMapping>(connectionString);
    }

    public static ISession OpenSession() => _internalHelper.OpenSession();

    public static IStatelessSession OpenStatelessSession() => _internalHelper.OpenStatelessSession();
}
