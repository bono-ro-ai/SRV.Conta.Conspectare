using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Conspectare.Services.Extraction;
public class ExtractionOrchestrationService
{
    private readonly IStorageService _storageService;
    private readonly IProcessorRegistry _processorRegistry;
    private readonly MultiModelExtractionService _multiModelService;
    private readonly MultiModelSettings _multiModelSettings;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly ConspectareMetrics _metrics;
    private readonly VatValidationService _vatValidationService;
    private readonly ILogger<ExtractionOrchestrationService> _logger;
    public ExtractionOrchestrationService(
        IStorageService storageService,
        IProcessorRegistry processorRegistry,
        MultiModelExtractionService multiModelService,
        IOptions<MultiModelSettings> multiModelSettings,
        DocumentStatusWorkflow workflow,
        ConspectareMetrics metrics,
        VatValidationService vatValidationService,
        ILogger<ExtractionOrchestrationService> logger)
    {
        _storageService = storageService;
        _processorRegistry = processorRegistry;
        _multiModelService = multiModelService;
        _multiModelSettings = multiModelSettings.Value;
        _workflow = workflow;
        _metrics = metrics;
        _vatValidationService = vatValidationService;
        _logger = logger;
    }
    public bool IsMultiModelEnabled => _multiModelSettings.Enabled;
    public async Task ProcessDocumentAsync(Document doc, CancellationToken ct)
    {
        var processor = _processorRegistry.Resolve(doc.InputFormat, doc.ContentType);
        var utcNow = DateTime.UtcNow;
        ExtractionResult result;
        try
        {
            await using var rawFileStream = await _storageService.DownloadAsync(doc.RawFileS3Key, ct);
            result = await processor.ExtractAsync(doc, rawFileStream, ct);
        }
        catch (Exception ex)
        {
            ExtractionErrorHandler.Handle(doc, _workflow, _metrics, ex, utcNow, _logger);
            return;
        }
        var hasReviewFlags = result.ReviewFlags is { Count: > 0 };
        var hasErrorFlags = result.ReviewFlags?.Any(f => f.Severity == ReviewFlagSeverity.Error) ?? false;
        var nextStatus = hasErrorFlags
            ? DocumentStatus.ReviewRequired
            : DocumentStatus.Completed;
        if (!_workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
        {
            _logger.LogError(
                "ExtractionWorker: invalid transition from {From} to {To} for document {DocumentId}",
                DocumentStatus.Extracting, nextStatus, doc.Id);
            return;
        }
        doc.Status = nextStatus;
        doc.UpdatedAt = utcNow;
        if (nextStatus == DocumentStatus.Completed)
            doc.CompletedAt = utcNow;
        var canonicalOutput = new CanonicalOutput
        {
            TenantId = doc.TenantId,
            SchemaVersion = result.SchemaVersion,
            OutputJson = result.OutputJson,
            CreatedAt = utcNow
        };
        CanonicalOutputDenormalizer.TryDenormalizeFields(canonicalOutput, result.OutputJson);
        var artifactS3Key = $"tenants/{doc.TenantId}/documents/{doc.Id}/llm_extraction_response.json";
        var responseBytes = Encoding.UTF8.GetBytes(result.OutputJson);
        using var responseStream = new MemoryStream(responseBytes);
        await _storageService.UploadAsync(artifactS3Key, responseStream, "application/json", ct);
        var artifact = new DocumentArtifact
        {
            TenantId = doc.TenantId,
            ArtifactType = ArtifactType.LlmExtractionResponse,
            S3Key = artifactS3Key,
            ContentType = "application/json",
            SizeBytes = responseBytes.Length,
            RetentionDays = 90,
            CreatedAt = utcNow
        };
        var attempt = new ExtractionAttempt
        {
            TenantId = doc.TenantId,
            AttemptNumber = doc.RetryCount + 1,
            Phase = PipelinePhase.Extraction,
            ModelId = result.ModelId,
            PromptVersion = result.PromptVersion,
            Status = ExtractionAttemptStatus.Completed,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            LatencyMs = result.LatencyMs,
            CreatedAt = utcNow,
            CompletedAt = utcNow
        };
        var statusDetails = hasReviewFlags
            ? $"Extraction completed with {result.ReviewFlags!.Count} review flag(s)"
            : "Extraction completed successfully";
        var statusEvent = new DocumentEvent
        {
            TenantId = doc.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = statusDetails,
            CreatedAt = utcNow
        };
        IList<ReviewFlag> reviewFlags = null;
        if (hasReviewFlags)
        {
            reviewFlags = result.ReviewFlags!.Select(f => new ReviewFlag
            {
                TenantId = doc.TenantId,
                FlagType = f.FlagType,
                Severity = f.Severity,
                Message = f.Message,
                IsResolved = false,
                CreatedAt = utcNow
            }).ToList<ReviewFlag>();
        }
        new SaveExtractionResultCommand(doc, canonicalOutput, attempt, statusEvent, artifact, reviewFlags).Execute();
        if (nextStatus == DocumentStatus.Completed)
            _metrics.RecordDocumentCompleted(PipelinePhase.Extraction);
        else if (nextStatus == DocumentStatus.ReviewRequired)
            _metrics.RecordDocumentCompleted("extraction_review_required");
        WebhookNotifier.NotifyIfNeeded(doc, _logger, canonicalOutput, reviewFlags);
        _logger.LogInformation(
            "ExtractionWorker: document {DocumentId} extracted -> {NextStatus} " +
            "(schema={SchemaVersion}, flags={FlagCount})",
            doc.Id, nextStatus, result.SchemaVersion, result.ReviewFlags?.Count ?? 0);
        // VAT validation runs async — does not block extraction pipeline
        doc.CanonicalOutput = canonicalOutput;
        _ = Task.Run(async () =>
        {
            try
            {
                await _vatValidationService.ValidateDocumentAsync(doc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ExtractionWorker: async VAT validation failed for document {DocumentId}",
                    doc.Id);
            }
        }, ct);
    }
    public async Task ProcessDocumentMultiModelAsync(Document doc, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        ConsensusResult consensus;
        byte[] rawFileBytes;
        try
        {
            await using var rawFileStream = await _storageService.DownloadAsync(doc.RawFileS3Key, ct);
            using var ms = new MemoryStream();
            await rawFileStream.CopyToAsync(ms, ct);
            rawFileBytes = ms.ToArray();
            consensus = await _multiModelService.ExtractAsync(doc, rawFileBytes, ct);
        }
        catch (Exception ex)
        {
            ExtractionErrorHandler.Handle(doc, _workflow, _metrics, ex, utcNow, _logger);
            return;
        }
        var winningResult = consensus.WinningResult;
        var hasReviewFlags = winningResult.ReviewFlags is { Count: > 0 };
        var hasErrorFlags = winningResult.ReviewFlags?.Any(f => f.Severity == ReviewFlagSeverity.Error) ?? false;
        var nextStatus = hasErrorFlags
            ? DocumentStatus.ReviewRequired
            : DocumentStatus.Completed;
        if (!_workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
        {
            _logger.LogError(
                "ExtractionWorker: invalid transition from {From} to {To} for document {DocumentId}",
                DocumentStatus.Extracting, nextStatus, doc.Id);
            return;
        }
        doc.Status = nextStatus;
        doc.UpdatedAt = utcNow;
        if (nextStatus == DocumentStatus.Completed)
            doc.CompletedAt = utcNow;
        var canonicalOutput = new CanonicalOutput
        {
            TenantId = doc.TenantId,
            SchemaVersion = winningResult.SchemaVersion,
            OutputJson = winningResult.OutputJson,
            ConsensusStrategy = consensus.StrategyUsed,
            WinningModelId = winningResult.ModelId,
            CreatedAt = utcNow
        };
        CanonicalOutputDenormalizer.TryDenormalizeFields(canonicalOutput, winningResult.OutputJson);
        var attempts = new List<ExtractionAttempt>();
        var artifacts = new List<DocumentArtifact>();
        foreach (var (providerKey, result) in consensus.AllResults)
        {
            var artifactS3Key = $"tenants/{doc.TenantId}/documents/{doc.Id}/llm_extraction_response_{providerKey}.json";
            var responseBytes = Encoding.UTF8.GetBytes(result.OutputJson);
            using var responseStream = new MemoryStream(responseBytes);
            await _storageService.UploadAsync(artifactS3Key, responseStream, "application/json", ct);
            artifacts.Add(new DocumentArtifact
            {
                TenantId = doc.TenantId,
                ArtifactType = ArtifactType.LlmExtractionResponse,
                S3Key = artifactS3Key,
                ContentType = "application/json",
                SizeBytes = responseBytes.Length,
                RetentionDays = 90,
                CreatedAt = utcNow
            });
            attempts.Add(new ExtractionAttempt
            {
                TenantId = doc.TenantId,
                AttemptNumber = doc.RetryCount + 1,
                Phase = PipelinePhase.Extraction,
                ModelId = result.ModelId,
                PromptVersion = result.PromptVersion,
                ProviderKey = providerKey,
                Status = providerKey == consensus.WinningProviderKey ? ExtractionAttemptStatus.Completed : ExtractionAttemptStatus.CompletedNonWinner,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                LatencyMs = result.LatencyMs,
                CreatedAt = utcNow,
                CompletedAt = utcNow
            });
        }
        var statusDetails = hasReviewFlags
            ? $"Multi-model extraction completed with {winningResult.ReviewFlags!.Count} review flag(s) (strategy={consensus.StrategyUsed}, winner={consensus.WinningProviderKey})"
            : $"Multi-model extraction completed successfully (strategy={consensus.StrategyUsed}, winner={consensus.WinningProviderKey})";
        var statusEvent = new DocumentEvent
        {
            TenantId = doc.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = statusDetails,
            CreatedAt = utcNow
        };
        IList<ReviewFlag> reviewFlags = null;
        if (hasReviewFlags)
        {
            reviewFlags = winningResult.ReviewFlags!.Select(f => new ReviewFlag
            {
                TenantId = doc.TenantId,
                FlagType = f.FlagType,
                Severity = f.Severity,
                Message = f.Message,
                IsResolved = false,
                CreatedAt = utcNow
            }).ToList<ReviewFlag>();
        }
        new SaveMultiModelExtractionResultCommand(doc, canonicalOutput, attempts, statusEvent, artifacts, reviewFlags).Execute();
        if (nextStatus == DocumentStatus.Completed)
            _metrics.RecordDocumentCompleted(PipelinePhase.Extraction);
        else if (nextStatus == DocumentStatus.ReviewRequired)
            _metrics.RecordDocumentCompleted("extraction_review_required");
        WebhookNotifier.NotifyIfNeeded(doc, _logger, canonicalOutput, reviewFlags);
        _logger.LogInformation(
            "ExtractionWorker: document {DocumentId} multi-model extracted -> {NextStatus} " +
            "(strategy={Strategy}, winner={Winner}, providers={ProviderCount})",
            doc.Id, nextStatus, consensus.StrategyUsed, consensus.WinningProviderKey, consensus.AllResults.Count);
        doc.CanonicalOutput = canonicalOutput;
        _ = Task.Run(async () =>
        {
            try
            {
                await _vatValidationService.ValidateDocumentAsync(doc, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ExtractionWorker: async VAT validation failed for document {DocumentId}",
                    doc.Id);
            }
        }, ct);
    }
}
