using System.Collections.Concurrent;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Infrastructure;

public class PipelineSignal : IPipelineSignal
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public void Signal(string stage)
    {
        var sem = _semaphores.GetOrAdd(stage, _ => new SemaphoreSlim(0));
        try
        {
            sem.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signaled; ignore.
        }
    }

    public async Task<bool> WaitAsync(string stage, TimeSpan timeout, CancellationToken ct)
    {
        var sem = _semaphores.GetOrAdd(stage, _ => new SemaphoreSlim(0));
        return await sem.WaitAsync(timeout, ct);
    }
}
