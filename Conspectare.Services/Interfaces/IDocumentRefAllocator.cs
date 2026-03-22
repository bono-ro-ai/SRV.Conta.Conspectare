using ISession = NHibernate.ISession;

namespace Conspectare.Services.Interfaces;

public interface IDocumentRefAllocator
{
    string AllocateRef(ISession session, string fiscalCode);
}
