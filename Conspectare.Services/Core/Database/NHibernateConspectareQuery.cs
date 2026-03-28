using Conspectare.Infrastructure.NHibernate.Queries;
using NHibernate;

namespace Conspectare.Services.Core.Database;

/// <summary>
/// Base class for read-only queries that return a typed result using a NHibernate session
/// against the Conspectare database.
/// </summary>
public abstract class NHibernateConspectareQuery<TResult> : NHibernateGenericQuery<TResult>
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}
