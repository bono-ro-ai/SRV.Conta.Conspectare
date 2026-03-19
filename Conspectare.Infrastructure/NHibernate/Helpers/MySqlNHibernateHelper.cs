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
    private Assembly _mappingAssembly;

    public INHibernateHelper Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false)
    {
        if (_configuration != null)
            return this;

        _mappingAssembly = typeof(TMapping).Assembly;
        _configuration = CreateConfiguration(connectionString, showSql, formatSql);
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

    public ISessionFactory SessionFactory
    {
        get
        {
            if (_sessionFactory != null)
                return _sessionFactory;

            lock (_syncLock)
                _sessionFactory = Fluently.Configure(_configuration)
                    .Mappings(m => m.FluentMappings.AddFromAssembly(_mappingAssembly))
                    .BuildSessionFactory();

            return _sessionFactory;
        }
    }

    private Configuration CreateConfiguration(string connectionString,
        bool showSql, bool formatSql)
    {
        var configuration = new Configuration();

        configuration.DataBaseIntegration(db =>
        {
            db.ConnectionProvider<DriverConnectionProvider>();
            db.Driver<global::NHibernate.Driver.MySqlConnector.MySqlConnectorDriver>();
            db.Dialect<MySQL5Dialect>();
            db.ConnectionString = connectionString;
            db.OrderInserts = true;
            db.LogSqlInConsole = showSql;
            db.LogFormattedSql = formatSql;
        });

        return configuration;
    }
}
