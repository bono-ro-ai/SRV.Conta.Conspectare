using NHibernate;

namespace Conspectare.Infrastructure.NHibernate.Commands;

public abstract class NHibernateGenericCommand<TResult>
{
    protected ISession Session { get; private set; }

    public NHibernateGenericCommand<TResult> UseExternalSession(ISession session)
    {
        Session = session;
        return this;
    }

    protected abstract ISession CreateSession();

    public TResult Execute()
    {
        if (Session != null)
        {
            return OnExecute();
        }

        TResult result;

        using (Session = CreateSession())
        using (var tran = Session.BeginTransaction())
        {
            result = OnExecute();
            tran.Commit();
        }

        return result;
    }

    protected abstract TResult OnExecute();
}
