using Conspectare.Domain.Enums;
using Conspectare.Services.Infrastructure;
using Xunit;

namespace Conspectare.Tests;

public class PipelineSignalTests
{
    [Fact]
    public async Task Signal_BeforeWait_ReturnsImmediately()
    {
        var signal = new PipelineSignal();

        signal.Signal(PipelinePhase.Triage);

        var result = await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Wait_WithoutSignal_TimesOut()
    {
        var signal = new PipelineSignal();

        var result = await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Concurrent_SignalAndWait_Works()
    {
        var signal = new PipelineSignal();

        var waitTask = Task.Run(async () =>
            await signal.WaitAsync(PipelinePhase.Extraction, TimeSpan.FromSeconds(5), CancellationToken.None));

        // Give the wait task a moment to start waiting
        await Task.Delay(50);

        signal.Signal(PipelinePhase.Extraction);

        var result = await waitTask;

        Assert.True(result);
    }

    [Fact]
    public async Task Multiple_Signals_Coalesce_EdgeTriggered()
    {
        var signal = new PipelineSignal();

        // Edge-triggered: multiple signals before any wait coalesce into one wake-up
        signal.Signal(PipelinePhase.Triage);
        signal.Signal(PipelinePhase.Triage);
        signal.Signal(PipelinePhase.Triage);

        var r1 = await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var r2 = await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.True(r1);   // First wait consumes the coalesced signal
        Assert.False(r2);  // No more signals — edge-triggered, maxCount=1
    }

    [Fact]
    public async Task Wait_Cancellation_ThrowsOperationCanceled()
    {
        var signal = new PipelineSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public async Task Different_Stages_AreIndependent()
    {
        var signal = new PipelineSignal();

        signal.Signal(PipelinePhase.Triage);

        var triageResult = await signal.WaitAsync(PipelinePhase.Triage, TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var extractionResult = await signal.WaitAsync(PipelinePhase.Extraction, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.True(triageResult);
        Assert.False(extractionResult);
    }
}
