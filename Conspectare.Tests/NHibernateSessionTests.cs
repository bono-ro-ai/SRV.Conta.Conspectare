using Conspectare.Tests.Helpers;
using FluentNHibernate.Cfg;
using Microsoft.Data.Sqlite;
using NHibernate.Cfg;
using NHibernate.Connection;
using NHibernate.Dialect;
using NHibernate.Tool.hbm2ddl;
using Conspectare.Infrastructure.Mappings;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class NHibernateSessionTests
{
    [Fact]
    public void SessionFactory_Build_Succeeds()
    {
        var factory = TestSessionFactory.Instance;

        Assert.NotNull(factory);
    }

    [Fact]
    public void OpenSession_CanExecuteQuery_ReturnsResult()
    {
        using var session = TestSessionFactory.OpenSession();

        var result = session.CreateSQLQuery("SELECT 1").UniqueResult();

        Assert.NotNull(result);
        Assert.Equal(1L, Convert.ToInt64(result));
    }

    [Fact]
    public void OpenSession_CloseSession_NoError()
    {
        var session = TestSessionFactory.OpenSession();

        session.Close();
        session.Dispose();
    }

    [Fact]
    public void SchemaExport_AllMappings_Succeeds()
    {
        var cfg = new Configuration();
        cfg.DataBaseIntegration(db =>
        {
            db.ConnectionProvider<DriverConnectionProvider>();
            db.Driver<MicrosoftDataSqliteDriver>();
            db.Dialect<SQLiteDialect>();
            db.ConnectionString = "Data Source=:memory:";
            db.KeywordsAutoImport = Hbm2DDLKeyWords.None;
        });

        var nhCfg = Fluently.Configure(cfg)
            .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ApiClientMap>())
            .BuildConfiguration();

        var export = new SchemaExport(nhCfg);

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        export.Execute(false, true, false, connection, null);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = cmd.ExecuteReader();

        var tables = new List<string>();
        while (reader.Read())
            tables.Add(reader.GetString(0));

        var expected = new[]
        {
            "cfg_api_clients",
            "pipe_documents",
            "pipe_document_artifacts",
            "pipe_extraction_attempts",
            "pipe_canonical_outputs",
            "pipe_document_events",
            "pipe_review_flags",
            "audit_job_executions"
        };

        foreach (var table in expected)
            Assert.Contains(table, tables);
    }
}
