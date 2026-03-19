using Conspectare.Infrastructure.NHibernate.Queries;
using NHibernate;

namespace Conspectare.Services.Core.Database;

public abstract class NHibernateConspectareQuery<TResult> : NHibernateGenericQuery<TResult>
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}
