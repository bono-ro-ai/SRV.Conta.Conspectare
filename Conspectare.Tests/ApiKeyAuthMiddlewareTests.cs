using System.Security.Cryptography;
using System.Text;
using Conspectare.Api.Middleware;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Conspectare.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using NHibernate;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class ApiKeyAuthMiddlewareTests
{
    private const string RawApiKey = "dp_test_abcdef1234567890";
    private static readonly string ApiKeyHash = Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(RawApiKey)));
    private const string ApiKeyPrefix = "dp_test_";

    private static ApiClient CreateActiveClient() => new()
    {
        Name = "Test Client",
        ApiKeyHash = ApiKeyHash,
        ApiKeyPrefix = ApiKeyPrefix,
        IsActive = true,
        RateLimitPerMin = 100,
        MaxFileSizeMb = 50,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static ApiClient CreateInactiveClient() => new()
    {
        Name = "Inactive Client",
        ApiKeyHash = ApiKeyHash,
        ApiKeyPrefix = ApiKeyPrefix,
        IsActive = false,
        RateLimitPerMin = 100,
        MaxFileSizeMb = 50,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static (HttpContext context, TenantContext tenantContext) CreateContext(string authHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (authHeader != null)
        {
            context.Request.Headers.Authorization = authHeader;
        }

        var tenantContext = new TenantContext();
        return (context, tenantContext);
    }

    private static ISessionFactory CreateSessionFactoryWithClient(ApiClient client)
    {
        var factory = TestSessionFactory.Instance;
        var session = TestSessionFactory.OpenSession();

        using (var tx = session.BeginTransaction())
        {
            session.Save(client);
            tx.Commit();
        }

        // Return a wrapper that always returns this session's connection
        return new TestSessionFactoryForAuth(session);
    }

    [Fact]
    public async Task MissingAuthorizationHeader_Returns401()
    {
        var middleware = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);
        var (context, tenantContext) = CreateContext();

        await middleware.InvokeAsync(context, TestSessionFactory.Instance, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task MalformedHeader_NoBearerPrefix_Returns401()
    {
        var middleware = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);
        var (context, tenantContext) = CreateContext("Basic some_token");

        await middleware.InvokeAsync(context, TestSessionFactory.Instance, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task ValidFormat_NoMatchingPrefix_Returns401()
    {
        var middleware = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);
        var (context, tenantContext) = CreateContext("Bearer dp_nomatch_1234567890");

        using var session = TestSessionFactory.OpenSession();
        var factory = new TestSessionFactoryForAuth(session);

        await middleware.InvokeAsync(context, factory, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task MatchingPrefix_WrongHash_Returns401()
    {
        var client = CreateActiveClient();
        using var session = TestSessionFactory.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            session.Save(client);
            tx.Commit();
        }

        var factory = new TestSessionFactoryForAuth(session);
        var middleware = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);
        var (context, tenantContext) = CreateContext("Bearer dp_test_WRONG_KEY_VALUE");

        await middleware.InvokeAsync(context, factory, tenantContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task CorrectHash_InactiveClient_Returns403()
    {
        var client = CreateInactiveClient();
        using var session = TestSessionFactory.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            session.Save(client);
            tx.Commit();
        }

        var factory = new TestSessionFactoryForAuth(session);
        var middleware = new ApiKeyAuthMiddleware(_ => Task.CompletedTask);
        var (context, tenantContext) = CreateContext($"Bearer {RawApiKey}");

        await middleware.InvokeAsync(context, factory, tenantContext);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task CorrectHash_ActiveClient_SetsTenantContextAndCallsNext()
    {
        var client = CreateActiveClient();
        using var session = TestSessionFactory.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            session.Save(client);
            tx.Commit();
        }

        var factory = new TestSessionFactoryForAuth(session);
        var nextCalled = false;
        var middleware = new ApiKeyAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, tenantContext) = CreateContext($"Bearer {RawApiKey}");

        await middleware.InvokeAsync(context, factory, tenantContext);

        Assert.True(nextCalled);
        Assert.Equal(client.Id, tenantContext.TenantId);
        Assert.Equal(ApiKeyPrefix, tenantContext.ApiKeyPrefix);
    }

    [Fact]
    public async Task ExemptPath_Health_SkipsAuth()
    {
        var nextCalled = false;
        var middleware = new ApiKeyAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, tenantContext) = CreateContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context, TestSessionFactory.Instance, tenantContext);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task ExemptPath_Swagger_SkipsAuth()
    {
        var nextCalled = false;
        var middleware = new ApiKeyAuthMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, tenantContext) = CreateContext();
        context.Request.Path = "/swagger/index.html";

        await middleware.InvokeAsync(context, TestSessionFactory.Instance, tenantContext);

        Assert.True(nextCalled);
    }
}

/// <summary>
/// Wraps the real ISessionFactory but overrides OpenSession() to reuse
/// the same in-memory SQLite connection (with pre-seeded data).
/// </summary>
internal class TestSessionFactoryForAuth : SessionFactoryDelegator
{
    private readonly ISession _sourceSession;

    public TestSessionFactoryForAuth(ISession sourceSession) : base(sourceSession.SessionFactory)
    {
        _sourceSession = sourceSession;
    }

    public override ISession OpenSession() =>
        Inner.WithOptions().Connection(_sourceSession.Connection).OpenSession();
}

/// <summary>
/// Delegates all ISessionFactory members to an inner factory. Subclasses override as needed.
/// </summary>
internal class SessionFactoryDelegator : ISessionFactory
{
    protected readonly ISessionFactory Inner;

    public SessionFactoryDelegator(ISessionFactory inner) => Inner = inner;

    public virtual void Dispose() => Inner.Dispose();
    public virtual ISession OpenSession() => Inner.OpenSession();
#pragma warning disable CS0618 // Obsolete members required by interface
    public ISession OpenSession(System.Data.Common.DbConnection connection) => Inner.OpenSession(connection);
    public ISession OpenSession(NHibernate.IInterceptor sessionLocalInterceptor) => Inner.OpenSession(sessionLocalInterceptor);
    public ISession OpenSession(System.Data.Common.DbConnection conn, NHibernate.IInterceptor sessionLocalInterceptor) => Inner.OpenSession(conn, sessionLocalInterceptor);
#pragma warning restore CS0618
    public IStatelessSession OpenStatelessSession() => Inner.OpenStatelessSession();
    public IStatelessSession OpenStatelessSession(System.Data.Common.DbConnection connection) => Inner.OpenStatelessSession(connection);
    public NHibernate.Metadata.IClassMetadata GetClassMetadata(Type persistentClass) => Inner.GetClassMetadata(persistentClass);
    public NHibernate.Metadata.IClassMetadata GetClassMetadata(string entityName) => Inner.GetClassMetadata(entityName);
    public NHibernate.Metadata.ICollectionMetadata GetCollectionMetadata(string roleName) => Inner.GetCollectionMetadata(roleName);
    public IDictionary<string, NHibernate.Metadata.IClassMetadata> GetAllClassMetadata() => Inner.GetAllClassMetadata();
    public IDictionary<string, NHibernate.Metadata.ICollectionMetadata> GetAllCollectionMetadata() => Inner.GetAllCollectionMetadata();
    public void Close() => Inner.Close();
    public void Evict(Type persistentClass) => Inner.Evict(persistentClass);
    public void Evict(Type persistentClass, object id) => Inner.Evict(persistentClass, id);
    public void EvictEntity(string entityName) => Inner.EvictEntity(entityName);
    public void EvictEntity(string entityName, object id) => Inner.EvictEntity(entityName, id);
    public void EvictCollection(string roleName) => Inner.EvictCollection(roleName);
    public void EvictCollection(string roleName, object id) => Inner.EvictCollection(roleName, id);
    public void EvictQueries() => Inner.EvictQueries();
    public void EvictQueries(string cacheRegion) => Inner.EvictQueries(cacheRegion);
    public NHibernate.Stat.IStatistics Statistics => Inner.Statistics;
    public bool IsClosed => Inner.IsClosed;
    public ICollection<string> DefinedFilterNames => Inner.DefinedFilterNames;
    public NHibernate.Engine.FilterDefinition GetFilterDefinition(string filterName) => Inner.GetFilterDefinition(filterName);
    public ISessionBuilder WithOptions() => Inner.WithOptions();
    public IStatelessSessionBuilder WithStatelessOptions() => Inner.WithStatelessOptions();
    public ISession GetCurrentSession() => Inner.GetCurrentSession();
    public Task CloseAsync(CancellationToken cancellationToken = default) => Inner.CloseAsync(cancellationToken);
    public Task EvictAsync(Type persistentClass, CancellationToken cancellationToken = default) => Inner.EvictAsync(persistentClass, cancellationToken);
    public Task EvictAsync(Type persistentClass, object id, CancellationToken cancellationToken = default) => Inner.EvictAsync(persistentClass, id, cancellationToken);
    public Task EvictEntityAsync(string entityName, CancellationToken cancellationToken = default) => Inner.EvictEntityAsync(entityName, cancellationToken);
    public Task EvictEntityAsync(string entityName, object id, CancellationToken cancellationToken = default) => Inner.EvictEntityAsync(entityName, id, cancellationToken);
    public Task EvictCollectionAsync(string roleName, CancellationToken cancellationToken = default) => Inner.EvictCollectionAsync(roleName, cancellationToken);
    public Task EvictCollectionAsync(string roleName, object id, CancellationToken cancellationToken = default) => Inner.EvictCollectionAsync(roleName, id, cancellationToken);
    public Task EvictQueriesAsync(CancellationToken cancellationToken = default) => Inner.EvictQueriesAsync(cancellationToken);
    public Task EvictQueriesAsync(string cacheRegion, CancellationToken cancellationToken = default) => Inner.EvictQueriesAsync(cacheRegion, cancellationToken);
}
