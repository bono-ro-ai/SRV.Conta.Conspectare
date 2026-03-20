using Conspectare.Infrastructure.NHibernate.Helpers;
using NHibernate;

namespace Conspectare.Services.Core.Database;

public class NHibernateConspectare
{
    private static INHibernateHelper _internalHelper;

    public static void Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false)
    {
        _internalHelper = new MySqlNHibernateHelper().Configure<TMapping>(connectionString, showSql, formatSql);
    }

    public static ISessionFactory SessionFactory => _internalHelper.SessionFactory;

    public static ISession OpenSession() => _internalHelper.OpenSession();

    public static IStatelessSession OpenStatelessSession() => _internalHelper.OpenStatelessSession();

    public static void ConfigureForTests(INHibernateHelper helper)
    {
        _internalHelper = helper;
    }
}
