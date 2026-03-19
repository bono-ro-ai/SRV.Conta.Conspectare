using Conspectare.Tests.Helpers;
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
}
