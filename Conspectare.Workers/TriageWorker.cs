using System.Diagnostics;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class TriageWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;
    private const decimal MinConfidenceThreshold = 0.7m;
    private readonly IPipelineSignal _pipelineSignal;
    protected override string JobName => "triage_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    protected override string SignalStage => "triage";
    public TriageWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<TriageWorker> logger,
        IPipelineSignal signal)
        : base(distributedLock, scopeFactory, logger, signal)
    {
        _pipelineSignal = signal;
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TriageWorker>>();
        var processorRegistry = scope.ServiceProvider.GetRequiredService<IProcessorRegistry>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var workflow = scope.ServiceProvider.GetRequiredService<DocumentStatusWorkflow>();
        var metrics = scope.ServiceProvider.GetRequiredService<ConspectareMetrics>();
        var pendingDocs = new FindPendingTriageDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;
        var claimedDocs = new ClaimDocumentsForTriageCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;
        foreach (var doc in claimedDocs)
            metrics.RecordDocumentIngested(doc.InputFormat ?? "unknown");
        var processedCount = 0;
        foreach (var doc in claimedDocs)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await ProcessDocumentAsync(doc, processorRegistry, storageService, workflow, metrics, logger, ct);
                sw.Stop();
                metrics.RecordProcessingDuration("triage", sw.ElapsedMilliseconds);
                processedCount++;
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordProcessingDuration("triage", sw.ElapsedMilliseconds);
                metrics.RecordDocumentFailed("triage", "unhandled_error");
                logger.LogError(ex,
                    "TriageWorker: failed to triage document {DocumentId}", doc.Id);
            }
        }
        return processedCount;
    }
    private async Task ProcessDocumentAsync(
        Document doc,
        IProcessorRegistry processorRegistry,
        IStorageService storageService,
        DocumentStatusWorkflow workflow,
        ConspectareMetrics metrics,
        ILogger logger,
        CancellationToken ct)
    {
        var processor = processorRegistry.Resolve(doc.InputFormat, doc.ContentType);
        await using var rawFileStream = await storageService.DownloadAsync(doc.RawFileS3Key, ct);
        var triageResult = await processor.TriageAsync(doc, rawFileStream, ct);
        var utcNow = DateTime.UtcNow;
        doc.DocumentType = triageResult.DocumentType;
        doc.TriageConfidence = triageResult.Confidence;
        doc.IsAccountingRelevant = triageResult.IsAccountingRelevant;
        doc.UpdatedAt = utcNow;
        var nextStatus = DetermineNextStatus(triageResult);
        if (!workflow.CanTransition(DocumentStatus.Triaging, nextStatus))
        {
            logger.LogError(
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
            Phase = "triage",
            ModelId = triageResult.ModelId,
            PromptVersion = triageResult.PromptVersion,
            Status = "completed",
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
            EventType = "status_change",
            FromStatus = DocumentStatus.Triaging,
            ToStatus = nextStatus,
            Details = $"Triage completed: type={triageResult.DocumentType}, " +
                      $"confidence={triageResult.Confidence:F2}, " +
                      $"accounting_relevant={triageResult.IsAccountingRelevant}",
            CreatedAt = utcNow
        };
        new SaveTriageResultCommand(doc, attempt, statusEvent).Execute();
        if (nextStatus == DocumentStatus.Rejected)
            metrics.RecordDocumentFailed("triage", "rejected");
        else if (nextStatus == DocumentStatus.ReviewRequired)
            metrics.RecordDocumentCompleted("triage_review_required");
        else if (nextStatus == DocumentStatus.PendingExtraction)
            metrics.RecordDocumentCompleted("triage");
        if (nextStatus is DocumentStatus.Rejected or DocumentStatus.ReviewRequired)
        {
            try
            {
                var client = new LoadApiClientByIdQuery(doc.TenantId).Execute();
                WebhookEnqueuer.EnqueueIfNeeded(doc, client, utcNow);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "TriageWorker: failed to enqueue webhook for document {DocumentId}", doc.Id);
            }
        }
        if (nextStatus == DocumentStatus.PendingExtraction)
            _pipelineSignal.Signal("extraction");
        logger.LogInformation(
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
