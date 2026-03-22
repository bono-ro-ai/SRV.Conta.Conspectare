using ISession = NHibernate.ISession;

namespace Conspectare.Services.Interfaces;

public interface IDocumentRefAllocator
{
    Task<string> AllocateRefAsync(ISession session, string fiscalCode);
}
