using NHibernate;

namespace Conspectare.Infrastructure.NHibernate.Queries;

public abstract class NHibernateGenericQuery<TResult>
{
    private ISession _externalSession;

    protected ISession Session { get; private set; }

    protected abstract ISession CreateSession();

    public NHibernateGenericQuery<TResult> UseExternalSession(ISession session)
    {
        _externalSession = session;
        return this;
    }

    public TResult Execute()
    {
        if (_externalSession != null)
        {
            Session = _externalSession;
            return OnExecute();
        }
        using var session = CreateSession();
        Session = session;
        return OnExecute();
    }

    protected abstract TResult OnExecute();
}
