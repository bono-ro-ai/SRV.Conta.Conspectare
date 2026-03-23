using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging;
namespace Conspectare.Services.Triage;
public class TriageOrchestrationService
{
    private const decimal MinConfidenceThreshold = 0.7m;
    private readonly IProcessorRegistry _processorRegistry;
    private readonly IStorageService _storageService;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly ConspectareMetrics _metrics;
    private readonly IPipelineSignal _pipelineSignal;
    private readonly ILogger<TriageOrchestrationService> _logger;
    public TriageOrchestrationService(
        IProcessorRegistry processorRegistry,
        IStorageService storageService,
        DocumentStatusWorkflow workflow,
        ConspectareMetrics metrics,
        IPipelineSignal pipelineSignal,
        ILogger<TriageOrchestrationService> logger)
    {
        _processorRegistry = processorRegistry;
        _storageService = storageService;
        _workflow = workflow;
        _metrics = metrics;
        _pipelineSignal = pipelineSignal;
        _logger = logger;
    }
    public async Task ProcessDocumentAsync(Document doc, CancellationToken ct)
    {
        var processor = _processorRegistry.Resolve(doc.InputFormat, doc.ContentType);
        await using var rawFileStream = await _storageService.DownloadAsync(doc.RawFileS3Key, ct);
        var triageResult = await processor.TriageAsync(doc, rawFileStream, ct);
        var utcNow = DateTime.UtcNow;
        doc.DocumentType = triageResult.DocumentType;
        doc.TriageConfidence = triageResult.Confidence;
        doc.IsAccountingRelevant = triageResult.IsAccountingRelevant;
        doc.UpdatedAt = utcNow;
        var nextStatus = DetermineNextStatus(triageResult);
        if (!_workflow.CanTransition(DocumentStatus.Triaging, nextStatus))
        {
            _logger.LogError(
                "TriageWorker: invalid transition from {From} to {To} for document {DocumentId}",
                DocumentStatus.Triaging, nextStatus, doc.Id);
            return;
        }
        doc.Status = nextStatus;
        var attempt = new ExtractionAttempt
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            AttemptNumber = 1,
            Phase = PipelinePhase.Triage,
            ModelId = triageResult.ModelId,
            PromptVersion = triageResult.PromptVersion,
            Status = ExtractionAttemptStatus.Completed,
            InputTokens = triageResult.InputTokens,
            OutputTokens = triageResult.OutputTokens,
            LatencyMs = triageResult.LatencyMs,
            Confidence = triageResult.Confidence,
            CreatedAt = utcNow,
            CompletedAt = utcNow
        };
        var statusEvent = new DocumentEvent
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = DocumentStatus.Triaging,
            ToStatus = nextStatus,
            Details = $"Triage completed: type={triageResult.DocumentType}, " +
                      $"confidence={triageResult.Confidence:F2}, " +
                      $"accounting_relevant={triageResult.IsAccountingRelevant}",
            CreatedAt = utcNow
        };
        new SaveTriageResultCommand(doc, attempt, statusEvent).Execute();
        if (nextStatus == DocumentStatus.Rejected)
            _metrics.RecordDocumentFailed(PipelinePhase.Triage, "rejected");
        else if (nextStatus == DocumentStatus.ReviewRequired)
            _metrics.RecordDocumentCompleted("triage_review_required");
        else if (nextStatus == DocumentStatus.PendingExtraction)
            _metrics.RecordDocumentCompleted(PipelinePhase.Triage);
        if (nextStatus is DocumentStatus.Rejected or DocumentStatus.ReviewRequired)
            WebhookNotifier.NotifyIfNeeded(doc, _logger);
        if (nextStatus == DocumentStatus.PendingExtraction)
            _pipelineSignal.Signal(PipelinePhase.Extraction);
        _logger.LogInformation(
            "TriageWorker: document {DocumentId} triaged -> {NextStatus} " +
            "(type={DocumentType}, confidence={Confidence:F2})",
            doc.Id, nextStatus, triageResult.DocumentType, triageResult.Confidence);
    }
    private static string DetermineNextStatus(TriageResult result)
    {
        if (!result.IsAccountingRelevant)
            return DocumentStatus.Rejected;
        if (result.Confidence < MinConfidenceThreshold)
            return DocumentStatus.ReviewRequired;
        return DocumentStatus.PendingExtraction;
    }
}
