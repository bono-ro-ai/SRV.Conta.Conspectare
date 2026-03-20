using System.Collections.Concurrent;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Infrastructure;

public sealed class PipelineSignal : IPipelineSignal, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public void Signal(string stage)
    {
        var sem = _semaphores.GetOrAdd(stage, _ => new SemaphoreSlim(0, 1));
        try
        {
            sem.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signaled; coalesce into single wake-up.
        }
    }

    public async Task<bool> WaitAsync(string stage, TimeSpan timeout, CancellationToken ct)
    {
        var sem = _semaphores.GetOrAdd(stage, _ => new SemaphoreSlim(0, 1));
        return await sem.WaitAsync(timeout, ct);
    }

    public void Dispose()
    {
        foreach (var sem in _semaphores.Values)
            sem.Dispose();
        _semaphores.Clear();
    }
}
