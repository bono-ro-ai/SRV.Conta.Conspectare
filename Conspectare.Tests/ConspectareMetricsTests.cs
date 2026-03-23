using System.Diagnostics.Metrics;
using Conspectare.Domain.Enums;
using Conspectare.Services.Observability;
using Xunit;

namespace Conspectare.Tests;

public class ConspectareMetricsTests
{
    private readonly ConspectareMetrics _metrics = new();

    [Fact]
    public void Meter_HasCorrectName()
    {
        Assert.Equal("Conspectare", _metrics.Meter.Name);
    }

    [Fact]
    public void Meter_HasCorrectVersion()
    {
        Assert.Equal("1.0", _metrics.Meter.Version);
    }

    [Fact]
    public void AllInstruments_AreRegistered()
    {
        var instrumentNames = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ConspectareMetrics.MeterName)
            {
                instrumentNames.Add(instrument.Name);
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.Start();
        var expected = new[]
        {
            "conspectare.documents.ingested",
            "conspectare.documents.completed",
            "conspectare.documents.failed",
            "conspectare.processing.duration",
            "conspectare.llm.call_duration",
            "conspectare.llm.tokens"
        };
        foreach (var name in expected)
            Assert.Contains(name, instrumentNames);
    }

    [Fact]
    public void RecordDocumentIngested_RecordsWithCorrectTags()
    {
        var recorded = CaptureCounter("conspectare.documents.ingested", () =>
            _metrics.RecordDocumentIngested("xml"));

        Assert.NotNull(recorded);
        Assert.Equal(1, recorded.Value);
        AssertTag(recorded.Tags, "input_format", "xml");
    }

    [Fact]
    public void RecordDocumentCompleted_RecordsWithCorrectTags()
    {
        var recorded = CaptureCounter("conspectare.documents.completed", () =>
            _metrics.RecordDocumentCompleted(PipelinePhase.Extraction));

        Assert.NotNull(recorded);
        Assert.Equal(1, recorded.Value);
        AssertTag(recorded.Tags, "phase", PipelinePhase.Extraction);
    }

    [Fact]
    public void RecordDocumentFailed_RecordsWithCorrectTags()
    {
        var recorded = CaptureCounter("conspectare.documents.failed", () =>
            _metrics.RecordDocumentFailed(PipelinePhase.Extraction, "max_retries_exceeded"));

        Assert.NotNull(recorded);
        Assert.Equal(1, recorded.Value);
        AssertTag(recorded.Tags, "phase", PipelinePhase.Extraction);
        AssertTag(recorded.Tags, "reason", "max_retries_exceeded");
    }

    [Fact]
    public void RecordProcessingDuration_RecordsWithCorrectTags()
    {
        var recorded = CaptureHistogram("conspectare.processing.duration", () =>
            _metrics.RecordProcessingDuration(PipelinePhase.Triage, 150.5));

        Assert.NotNull(recorded);
        Assert.Equal(150.5, recorded.Value);
        AssertTag(recorded.Tags, "phase", PipelinePhase.Triage);
    }

    [Fact]
    public void RecordLlmCallDuration_RecordsWithCorrectTags()
    {
        var recorded = CaptureHistogram("conspectare.llm.call_duration", () =>
            _metrics.RecordLlmCallDuration("claude", PipelinePhase.Triage, 2500.0));

        Assert.NotNull(recorded);
        Assert.Equal(2500.0, recorded.Value);
        AssertTag(recorded.Tags, "provider", "claude");
        AssertTag(recorded.Tags, "operation", PipelinePhase.Triage);
    }

    [Fact]
    public void RecordLlmTokens_RecordsWithCorrectTags()
    {
        var recorded = CaptureCounter("conspectare.llm.tokens", () =>
            _metrics.RecordLlmTokens("gemini", "input", 1500));

        Assert.NotNull(recorded);
        Assert.Equal(1500, recorded.Value);
        AssertTag(recorded.Tags, "provider", "gemini");
        AssertTag(recorded.Tags, "token_type", "input");
    }

    private CapturedMeasurement<long> CaptureCounter(string instrumentName, Action action)
    {
        CapturedMeasurement<long> result = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ConspectareMetrics.MeterName && instrument.Name == instrumentName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == instrumentName)
                result = new CapturedMeasurement<long>(value, tags.ToArray());
        });
        listener.Start();
        action();
        return result;
    }

    private CapturedMeasurement<double> CaptureHistogram(string instrumentName, Action action)
    {
        CapturedMeasurement<double> result = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ConspectareMetrics.MeterName && instrument.Name == instrumentName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            if (instrument.Name == instrumentName)
                result = new CapturedMeasurement<double>(value, tags.ToArray());
        });
        listener.Start();
        action();
        return result;
    }

    private static void AssertTag<T>(KeyValuePair<string, object>[] tags, string key, T expectedValue)
    {
        var tag = tags.FirstOrDefault(t => t.Key == key);
        Assert.NotNull(tag.Key);
        Assert.Equal(expectedValue, (T)tag.Value);
    }

    private record CapturedMeasurement<T>(T Value, KeyValuePair<string, object>[] Tags);
}
