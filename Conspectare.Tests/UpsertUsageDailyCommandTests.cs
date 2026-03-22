using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.Queries;
using Conspectare.Tests.Helpers;
using NHibernate;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class UpsertUsageDailyCommandTests : IDisposable
{
    private readonly TestNHibernateHelper _db;

    public UpsertUsageDailyCommandTests()
    {
        _db = new TestNHibernateHelper();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Execute_NoExistingRow_InsertsNew()
    {
        var aggregate = new UsageAggregateResult
        {
            DocumentsIngested = 10,
            DocumentsProcessed = 8,
            LlmInputTokens = 5000,
            LlmOutputTokens = 3000,
            LlmRequests = 12,
            StorageBytes = 1024000,
            ApiCalls = 10
        };
        var date = new DateTime(2026, 3, 21);

        using (var session = _db.OpenSession())
        using (var tx = session.BeginTransaction())
        {
            var cmd = new UpsertUsageDailyCommand(1, date, aggregate);
            cmd.UseExternalSession(session).Execute();
            tx.Commit();
        }

        using var verifySession = _db.OpenSession();
        var rows = verifySession.QueryOver<UsageDaily>().List();
        Assert.Single(rows);
        Assert.Equal(10, rows[0].DocumentsIngested);
        Assert.Equal(8, rows[0].DocumentsProcessed);
        Assert.Equal(5000, rows[0].LlmInputTokens);
        Assert.Equal(3000, rows[0].LlmOutputTokens);
        Assert.Equal(12, rows[0].LlmRequests);
        Assert.Equal(1024000, rows[0].StorageBytes);
        Assert.Equal(10, rows[0].ApiCalls);
    }

    [Fact]
    public void Execute_ExistingRow_Updates()
    {
        var date = new DateTime(2026, 3, 21);

        using (var session = _db.OpenSession())
        using (var tx = session.BeginTransaction())
        {
            session.Save(new UsageDaily
            {
                TenantId = 1,
                UsageDate = date,
                DocumentsIngested = 5,
                DocumentsProcessed = 3,
                LlmInputTokens = 1000,
                LlmOutputTokens = 500,
                LlmRequests = 4,
                StorageBytes = 512000,
                ApiCalls = 5,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            tx.Commit();
        }

        var updatedAggregate = new UsageAggregateResult
        {
            DocumentsIngested = 15,
            DocumentsProcessed = 12,
            LlmInputTokens = 8000,
            LlmOutputTokens = 6000,
            LlmRequests = 20,
            StorageBytes = 2048000,
            ApiCalls = 15
        };

        using (var session = _db.OpenSession())
        using (var tx = session.BeginTransaction())
        {
            new UpsertUsageDailyCommand(1, date, updatedAggregate).UseExternalSession(session).Execute();
            tx.Commit();
        }

        using var verifySession = _db.OpenSession();
        var rows = verifySession.QueryOver<UsageDaily>().List();
        Assert.Single(rows);
        Assert.Equal(15, rows[0].DocumentsIngested);
        Assert.Equal(12, rows[0].DocumentsProcessed);
        Assert.Equal(8000, rows[0].LlmInputTokens);
    }
}
