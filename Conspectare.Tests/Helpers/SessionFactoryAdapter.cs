using NHibernate;
using NHibernate.Engine;
using NHibernate.Metadata;
using NHibernate.Stat;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

/// <summary>
/// Minimal ISessionFactory adapter that delegates OpenSession() to a SharedConnectionSessionFactory
/// so all sessions share the same in-memory SQLite connection. All other members throw NotImplementedException
/// since DocumentService only uses OpenSession().
/// </summary>
public sealed class SessionFactoryAdapter : ISessionFactory
{
    private readonly SharedConnectionSessionFactory _shared;

    public SessionFactoryAdapter(SharedConnectionSessionFactory shared)
    {
        _shared = shared;
    }

    public ISession OpenSession() => _shared.OpenSession();

    // The following members are not used by DocumentService but required by the interface.
    public void Dispose() { }
    public ISession OpenSession(System.Data.Common.DbConnection connection) => throw new NotImplementedException();
    public ISession OpenSession(IInterceptor sessionLocalInterceptor) => throw new NotImplementedException();
    public ISession OpenSession(System.Data.Common.DbConnection conn, IInterceptor sessionLocalInterceptor) => throw new NotImplementedException();
    public IStatelessSession OpenStatelessSession() => throw new NotImplementedException();
    public IStatelessSession OpenStatelessSession(System.Data.Common.DbConnection connection) => throw new NotImplementedException();
    public IClassMetadata GetClassMetadata(Type persistentClass) => throw new NotImplementedException();
    public IClassMetadata GetClassMetadata(string entityName) => throw new NotImplementedException();
    public ICollectionMetadata GetCollectionMetadata(string roleName) => throw new NotImplementedException();
    public IDictionary<string, IClassMetadata> GetAllClassMetadata() => throw new NotImplementedException();
    public IDictionary<string, ICollectionMetadata> GetAllCollectionMetadata() => throw new NotImplementedException();
    public void Close() { }
    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Evict(Type persistentClass) { }
    public Task EvictAsync(Type persistentClass, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Evict(Type persistentClass, object id) { }
    public Task EvictAsync(Type persistentClass, object id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictEntity(string entityName) { }
    public Task EvictEntityAsync(string entityName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictEntity(string entityName, object id) { }
    public Task EvictEntityAsync(string entityName, object id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictCollection(string roleName) { }
    public Task EvictCollectionAsync(string roleName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictCollection(string roleName, object id) { }
    public Task EvictCollectionAsync(string roleName, object id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictQueries() { }
    public Task EvictQueriesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void EvictQueries(string cacheRegion) { }
    public Task EvictQueriesAsync(string cacheRegion, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IStatistics Statistics => throw new NotImplementedException();
    public bool IsClosed => false;
    public ICollection<string> DefinedFilterNames => throw new NotImplementedException();
    public FilterDefinition GetFilterDefinition(string filterName) => throw new NotImplementedException();
    public ISessionBuilder WithOptions() => throw new NotImplementedException();
    public IStatelessSessionBuilder WithStatelessOptions() => throw new NotImplementedException();
    public ISession GetCurrentSession() => throw new NotImplementedException();
}
