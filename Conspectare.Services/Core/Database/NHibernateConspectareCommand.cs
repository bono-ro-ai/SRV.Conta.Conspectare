using Conspectare.Infrastructure.NHibernate.Commands;
using NHibernate;

namespace Conspectare.Services.Core.Database;

public abstract class NHibernateConspectareCommand : NHibernateCommand
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}

public abstract class NHibernateConspectareCommand<T> : NHibernateGenericCommand<T>
{
    protected override ISession CreateSession() => NHibernateConspectare.OpenSession();
}

public class SaveOrUpdateCommand : NHibernateConspectareCommand
{
    private object _entity;
    public static SaveOrUpdateCommand For(object entity) => new() { _entity = entity };
    protected override void OnExecute() => Session.SaveOrUpdate(_entity);
}
