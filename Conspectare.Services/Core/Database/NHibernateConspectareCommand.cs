using Conspectare.Infrastructure.NHibernate.Commands;
using NHibernate;

namespace Conspectare.Services.Core.Database;

/// <summary>
/// Base class for void commands that require a transactional NHibernate session against the Conspectare database.
/// </summary>
public abstract class NHibernateConspectareCommand : NHibernateCommand
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}

/// <summary>
/// Base class for commands that return a typed result and require a transactional NHibernate session
/// against the Conspectare database.
/// </summary>
public abstract class NHibernateConspectareCommand<T> : NHibernateGenericCommand<T>
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}

/// <summary>
/// Convenience command that saves or updates a single entity within a transaction.
/// Use <see cref="For"/> to construct an instance with the target entity.
/// </summary>
public class SaveOrUpdateCommand : NHibernateConspectareCommand
{
    private object _entity;

    /// <summary>
    /// Creates a <see cref="SaveOrUpdateCommand"/> targeting the given entity.
    /// </summary>
    public static SaveOrUpdateCommand For(object entity) => new() { _entity = entity };

    protected override void OnExecute() => Session.SaveOrUpdate(_entity);
}
