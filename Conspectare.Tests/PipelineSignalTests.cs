using Conspectare.Services.Infrastructure;
using Xunit;

namespace Conspectare.Tests;

public class PipelineSignalTests
{
    [Fact]
    public async Task Signal_BeforeWait_ReturnsImmediately()
    {
        var signal = new PipelineSignal();

        signal.Signal("triage");

        var result = await signal.WaitAsync("triage", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task Wait_WithoutSignal_TimesOut()
    {
        var signal = new PipelineSignal();

        var result = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task Concurrent_SignalAndWait_Works()
    {
        var signal = new PipelineSignal();

        var waitTask = Task.Run(async () =>
            await signal.WaitAsync("extraction", TimeSpan.FromSeconds(5), CancellationToken.None));

        // Give the wait task a moment to start waiting
        await Task.Delay(50);

        signal.Signal("extraction");

        var result = await waitTask;

        Assert.True(result);
    }

    [Fact]
    public async Task Multiple_Signals_Accumulated()
    {
        var signal = new PipelineSignal();

        signal.Signal("triage");
        signal.Signal("triage");
        signal.Signal("triage");

        var r1 = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var r2 = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var r3 = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var r4 = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.True(r1);
        Assert.True(r2);
        Assert.True(r3);
        Assert.False(r4); // No more signals accumulated
    }

    [Fact]
    public async Task Wait_Cancellation_ThrowsOperationCanceled()
    {
        var signal = new PipelineSignal();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await signal.WaitAsync("triage", TimeSpan.FromSeconds(30), cts.Token));
    }

    [Fact]
    public async Task Different_Stages_AreIndependent()
    {
        var signal = new PipelineSignal();

        signal.Signal("triage");

        var triageResult = await signal.WaitAsync("triage", TimeSpan.FromMilliseconds(50), CancellationToken.None);
        var extractionResult = await signal.WaitAsync("extraction", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.True(triageResult);
        Assert.False(extractionResult);
    }
}
