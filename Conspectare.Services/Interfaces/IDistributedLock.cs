#nullable enable

namespace Conspectare.Services.Interfaces;

public interface IDistributedLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(string lockName, CancellationToken ct = default);
}
