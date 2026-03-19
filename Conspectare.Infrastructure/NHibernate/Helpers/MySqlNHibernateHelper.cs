using System.Reflection;
using FluentNHibernate.Cfg;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Connection;
using NHibernate.Dialect;

namespace Conspectare.Infrastructure.NHibernate.Helpers;

public class MySqlNHibernateHelper : INHibernateHelper
{
    private readonly Lock _syncLock = new();
    private Configuration _configuration;
    private ISessionFactory _sessionFactory;

    public INHibernateHelper Configure<TMapping>(string connectionString)
    {
        if (_configuration != null)
            return this;

        _configuration = CreateConfiguration(typeof(TMapping).Assembly, connectionString);
        return this;
    }

    public ISession OpenSession()
    {
        return SessionFactory.OpenSession();
    }

    public IStatelessSession OpenStatelessSession()
    {
        return SessionFactory.OpenStatelessSession();
    }

    private ISessionFactory SessionFactory
    {
        get
        {
            if (_sessionFactory != null)
                return _sessionFactory;

            lock (_syncLock)
                _sessionFactory = Fluently.Configure(_configuration)
                    .Mappings(m => m.FluentMappings.AddFromAssembly(_configuration.ClassMappings
                        .Select(c => c.MappedClass.Assembly).FirstOrDefault() ?? Assembly.GetExecutingAssembly()))
                    .BuildSessionFactory();

            return _sessionFactory;
        }
    }

    private Configuration CreateConfiguration(Assembly assembly, string connectionString)
    {
        var configuration = new Configuration();

        configuration.DataBaseIntegration(db =>
        {
            db.ConnectionProvider<DriverConnectionProvider>();
            db.Driver<global::NHibernate.Driver.MySqlConnector.MySqlConnectorDriver>();
            db.Dialect<MySQL5Dialect>();
            db.ConnectionString = connectionString;
            db.OrderInserts = true;
            db.LogSqlInConsole = false;
            db.LogFormattedSql = true;
        });

        return configuration;
    }
}
