using Conspectare.Infrastructure.Mappings;
using FluentNHibernate.Cfg;
using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Tool.hbm2ddl;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests.Helpers;

public static class TestSessionFactory
{
    private static readonly Lazy<(ISessionFactory Factory, Configuration Cfg)> _lazy = new(Build);

    public static ISessionFactory Instance => _lazy.Value.Factory;
    public static Configuration Configuration => _lazy.Value.Cfg;

    public static ISession OpenSession()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var session = Instance.WithOptions().Connection(connection).OpenSession();
        new SchemaExport(_lazy.Value.Cfg).Execute(false, true, false, connection, null);

        return session;
    }

    private static (ISessionFactory, Configuration) Build()
    {
        var cfg = new Configuration();
        cfg.DataBaseIntegration(db =>
        {
            db.ConnectionProvider<DriverConnectionProvider>();
            db.Driver<MicrosoftDataSqliteDriver>();
            db.Dialect<SQLiteDialect>();
            db.ConnectionString = "Data Source=:memory:";
            db.LogSqlInConsole = true;
            db.KeywordsAutoImport = Hbm2DDLKeyWords.None;
            db.SchemaAction = SchemaAutoAction.Recreate;
        });

        var factory = Fluently.Configure(cfg)
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ApiClientMap>())
            .BuildSessionFactory();

        return (factory, cfg);
    }
}
