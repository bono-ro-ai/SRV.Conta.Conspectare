namespace Conspectare.Services.Interfaces;

public interface IPipelineSignal
{
    void Signal(string stage);
    Task<bool> WaitAsync(string stage, TimeSpan timeout, CancellationToken ct);
}
