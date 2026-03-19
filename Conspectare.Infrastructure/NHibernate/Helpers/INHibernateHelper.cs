using NHibernate;

namespace Conspectare.Infrastructure.NHibernate.Helpers;

public interface INHibernateHelper
{
    INHibernateHelper Configure<TMapping>(string connectionString);
    ISession OpenSession();
    IStatelessSession OpenStatelessSession();
}
