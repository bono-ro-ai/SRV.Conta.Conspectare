using System.Collections.Concurrent;
using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Infrastructure;

/// <summary>
/// In-process pub/sub signal bus used to wake pipeline background workers when new work arrives,
/// avoiding the need to poll on a fixed interval.
/// Each pipeline stage has its own <see cref="SemaphoreSlim"/> keyed by stage name.
/// Multiple rapid signals for the same stage are coalesced into a single wake-up.
/// </summary>
public sealed class PipelineSignal : IPipelineSignal, IDisposable
{
    // One semaphore per named stage; created lazily on first use.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    /// <summary>
    /// Signals that work is available for the given <paramref name="stage"/>.
    /// If the stage has already been signaled but not yet consumed, the extra signal is dropped
    /// (max count = 1 ensures the semaphore never exceeds 1).
    /// </summary>
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

    /// <summary>
    /// Waits until a signal is received for <paramref name="stage"/>, the <paramref name="timeout"/> elapses,
    /// or <paramref name="ct"/> is cancelled — whichever comes first.
    /// Returns <c>true</c> if the semaphore was entered (signal received), <c>false</c> on timeout.
    /// </summary>
    public async Task<bool> WaitAsync(string stage, TimeSpan timeout, CancellationToken ct)
    {
        var sem = _semaphores.GetOrAdd(stage, _ => new SemaphoreSlim(0, 1));
        return await sem.WaitAsync(timeout, ct);
    }

    /// <summary>
    /// Disposes all semaphores tracked by this instance.
    /// </summary>
    public void Dispose()
    {
        foreach (var sem in _semaphores.Values)
            sem.Dispose();

        _semaphores.Clear();
    }
}
