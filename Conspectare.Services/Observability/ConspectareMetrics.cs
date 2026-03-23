using System.Diagnostics;
using System.Diagnostics.Metrics;
namespace Conspectare.Services.Observability;
public class ConspectareMetrics : IDisposable
{
    public const string MeterName = "Conspectare";
    private readonly Counter<long> _documentsIngested;
    private readonly Counter<long> _documentsCompleted;
    private readonly Counter<long> _documentsFailed;
    private readonly Histogram<double> _processingDuration;
    private readonly Histogram<double> _llmCallDuration;
    private readonly Counter<long> _llmTokens;
    private readonly Counter<long> _memoryRecyclingTriggered;
    public Meter Meter { get; }
    public ConspectareMetrics()
    {
        Meter = new Meter(MeterName, "1.0");
        _documentsIngested = Meter.CreateCounter<long>(
            "conspectare.documents.ingested",
            description: "Number of documents ingested into the pipeline");
        _documentsCompleted = Meter.CreateCounter<long>(
            "conspectare.documents.completed",
            description: "Number of documents that reached a terminal status");
        _documentsFailed = Meter.CreateCounter<long>(
            "conspectare.documents.failed",
            description: "Number of documents that failed processing");
        _processingDuration = Meter.CreateHistogram<double>(
            "conspectare.processing.duration",
            unit: "ms",
            description: "Time spent processing a document in a given phase");
        _llmCallDuration = Meter.CreateHistogram<double>(
            "conspectare.llm.call_duration",
            unit: "ms",
            description: "Duration of individual LLM API calls");
        _llmTokens = Meter.CreateCounter<long>(
            "conspectare.llm.tokens",
            description: "Number of tokens consumed by LLM calls");
        _memoryRecyclingTriggered = Meter.CreateCounter<long>(
            "conspectare.memory.recycling_triggered",
            description: "Number of times memory recycling GC was triggered");
        Meter.CreateObservableGauge(
            "conspectare.memory.heap_size_bytes",
            () => GC.GetTotalMemory(false),
            unit: "bytes",
            description: "Managed heap size in bytes");
        Meter.CreateObservableGauge(
            "conspectare.memory.working_set_bytes",
            () => Process.GetCurrentProcess().WorkingSet64,
            unit: "bytes",
            description: "Process working set in bytes");
    }
    public void Dispose()
    {
        Meter.Dispose();
        GC.SuppressFinalize(this);
    }
    public void RecordDocumentIngested(string inputFormat)
    {
        _documentsIngested.Add(1,
            new KeyValuePair<string, object>("input_format", inputFormat));
    }
    public void RecordDocumentCompleted(string phase)
    {
        _documentsCompleted.Add(1,
            new KeyValuePair<string, object>("phase", phase));
    }
    public void RecordDocumentFailed(string phase, string reason)
    {
        _documentsFailed.Add(1,
            new KeyValuePair<string, object>("phase", phase),
            new KeyValuePair<string, object>("reason", reason));
    }
    public void RecordProcessingDuration(string phase, double durationMs)
    {
        _processingDuration.Record(durationMs,
            new KeyValuePair<string, object>("phase", phase));
    }
    public void RecordLlmCallDuration(string provider, string operation, double durationMs)
    {
        _llmCallDuration.Record(durationMs,
            new KeyValuePair<string, object>("provider", provider),
            new KeyValuePair<string, object>("operation", operation));
    }
    public void RecordLlmTokens(string provider, string tokenType, long count)
    {
        _llmTokens.Add(count,
            new KeyValuePair<string, object>("provider", provider),
            new KeyValuePair<string, object>("token_type", tokenType));
    }
    public void RecordMemoryRecyclingTriggered(long heapBeforeBytes, long heapAfterBytes)
    {
        _memoryRecyclingTriggered.Add(1,
            new KeyValuePair<string, object>("heap_before_bytes", heapBeforeBytes),
            new KeyValuePair<string, object>("heap_after_bytes", heapAfterBytes));
    }
}
