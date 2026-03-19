using NHibernate;

namespace Conspectare.Infrastructure.NHibernate.Helpers;

public interface INHibernateHelper
{
    INHibernateHelper Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false);
    ISessionFactory SessionFactory { get; }
    ISession OpenSession();
    IStatelessSession OpenStatelessSession();
}
