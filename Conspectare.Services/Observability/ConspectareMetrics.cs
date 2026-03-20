using System.Diagnostics.Metrics;
namespace Conspectare.Services.Observability;
public class ConspectareMetrics
{
    public const string MeterName = "Conspectare";
    private readonly Counter<long> _documentsIngested;
    private readonly Counter<long> _documentsCompleted;
    private readonly Counter<long> _documentsFailed;
    private readonly Histogram<double> _processingDuration;
    private readonly Histogram<double> _llmCallDuration;
    private readonly Counter<long> _llmTokens;
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
    }
    public void RecordDocumentIngested(long tenantId, string inputFormat)
    {
        _documentsIngested.Add(1,
            new KeyValuePair<string, object>("tenant_id", tenantId),
            new KeyValuePair<string, object>("input_format", inputFormat));
    }
    public void RecordDocumentCompleted(long tenantId, string phase)
    {
        _documentsCompleted.Add(1,
            new KeyValuePair<string, object>("tenant_id", tenantId),
            new KeyValuePair<string, object>("phase", phase));
    }
    public void RecordDocumentFailed(long tenantId, string phase, string reason)
    {
        _documentsFailed.Add(1,
            new KeyValuePair<string, object>("tenant_id", tenantId),
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
}
