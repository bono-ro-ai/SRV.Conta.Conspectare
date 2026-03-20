using Conspectare.Infrastructure.NHibernate.Helpers;
using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests.Helpers;

public sealed class TestNHibernateHelper : INHibernateHelper, IDisposable
{
    private readonly SqliteConnection _connection;

    public TestNHibernateHelper()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var session = TestSessionFactory.Instance.WithOptions().Connection(_connection).OpenSession();
        new SchemaExport(TestSessionFactory.Configuration).Execute(false, true, false, _connection, null);
        session.Dispose();
    }

    public INHibernateHelper Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false)
    {
        return this;
    }

    public ISessionFactory SessionFactory => TestSessionFactory.Instance;

    public ISession OpenSession()
    {
        return SessionFactory.WithOptions().Connection(_connection).OpenSession();
    }

    public IStatelessSession OpenStatelessSession()
    {
        return SessionFactory.WithStatelessOptions().Connection(_connection).OpenStatelessSession();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
