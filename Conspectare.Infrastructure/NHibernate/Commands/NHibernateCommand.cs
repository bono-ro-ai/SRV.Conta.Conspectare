using NHibernate;

namespace Conspectare.Infrastructure.NHibernate.Commands;

public abstract class NHibernateCommand
{
    protected ISession Session { get; private set; }

    public NHibernateCommand UseExternalSession(ISession session)
    {
        Session = session;
        return this;
    }

    protected abstract ISession CreateSession();

    public void Execute()
    {
        if (Session != null)
        {
            OnExecute();
        }
        else
        {
            using (Session = CreateSession())
            using (var tran = Session.BeginTransaction())
            {
                OnExecute();
                tran.Commit();
            }
        }
    }

    protected abstract void OnExecute();
}
