using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Commands;
using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Conspectare.Services.Processors.Models;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Conspectare.Workers;
public class ExtractionWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;
    protected override string JobName => "extraction_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    protected override string SignalStage => "extraction";
    private readonly MultiModelSettings _multiModelSettings;
    public ExtractionWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        IOptions<MultiModelSettings> multiModelSettings,
        ILogger<ExtractionWorker> logger,
        IPipelineSignal signal)
        : base(distributedLock, scopeFactory, logger, signal)
    {
        _multiModelSettings = multiModelSettings.Value;
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExtractionWorker>>();
        var processorRegistry = scope.ServiceProvider.GetRequiredService<IProcessorRegistry>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var workflow = scope.ServiceProvider.GetRequiredService<DocumentStatusWorkflow>();
        var vatValidationService = scope.ServiceProvider.GetRequiredService<VatValidationService>();
        var metrics = scope.ServiceProvider.GetRequiredService<ConspectareMetrics>();
        var pendingDocs = new FindPendingExtractionDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;
        var claimedDocs = new ClaimDocumentsForExtractionCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;
        var processedCount = 0;
        foreach (var doc in claimedDocs)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (_multiModelSettings.Enabled)
                {
                    var multiModelService = scope.ServiceProvider.GetRequiredService<MultiModelExtractionService>();
                    await ProcessDocumentMultiModelAsync(doc, multiModelService, storageService, workflow, vatValidationService, metrics, logger, ct);
                }
                else
                {
                    await ProcessDocumentAsync(doc, processorRegistry, storageService, workflow, vatValidationService, metrics, logger, ct);
                }
                sw.Stop();
                metrics.RecordProcessingDuration("extraction", sw.ElapsedMilliseconds);
                processedCount++;
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordProcessingDuration("extraction", sw.ElapsedMilliseconds);
                metrics.RecordDocumentFailed("extraction", "unhandled_error");
                logger.LogError(ex,
                    "ExtractionWorker: unhandled error for document {DocumentId}", doc.Id);
            }
        }
        return processedCount;
    }
    private static async Task ProcessDocumentAsync(
        Document doc,
        IProcessorRegistry processorRegistry,
        IStorageService storageService,
        DocumentStatusWorkflow workflow,
        VatValidationService vatValidationService,
        ConspectareMetrics metrics,
        ILogger logger,
        CancellationToken ct)
    {
        var processor = processorRegistry.Resolve(doc.InputFormat, doc.ContentType);
        var utcNow = DateTime.UtcNow;
        ExtractionResult result;
        try
        {
            await using var rawFileStream = await storageService.DownloadAsync(doc.RawFileS3Key, ct);
            result = await processor.ExtractAsync(doc, rawFileStream, ct);
        }
        catch (Exception ex)
        {
            HandleExtractionError(doc, workflow, metrics, ex, utcNow, logger);
            return;
        }
        var validationFindings = RunPostExtractionValidation(result.OutputJson, logger);
        var allFlags = new List<ReviewFlagInfo>();
        if (result.ReviewFlags is { Count: > 0 })
            allFlags.AddRange(result.ReviewFlags);
        allFlags.AddRange(validationFindings);
        var hasReviewFlags = allFlags.Count > 0;
        var nextStatus = hasReviewFlags
            ? DocumentStatus.ReviewRequired
            : DocumentStatus.Completed;
        if (!workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
        {
            logger.LogError(
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
        TryDenormalizeFields(canonicalOutput, result.OutputJson);
        var artifactS3Key = $"tenants/{doc.TenantId}/documents/{doc.Id}/llm_extraction_response.json";
        var responseBytes = Encoding.UTF8.GetBytes(result.OutputJson);
        using var responseStream = new MemoryStream(responseBytes);
        await storageService.UploadAsync(artifactS3Key, responseStream, "application/json", ct);
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
            Phase = "extraction",
            ModelId = result.ModelId,
            PromptVersion = result.PromptVersion,
            Status = "completed",
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
            LatencyMs = result.LatencyMs,
            CreatedAt = utcNow,
            CompletedAt = utcNow
        };
        var statusDetails = hasReviewFlags
            ? $"Extraction completed with {allFlags.Count} review flag(s)"
            : "Extraction completed successfully";
        var statusEvent = new DocumentEvent
        {
            TenantId = doc.TenantId,
            EventType = "status_change",
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = statusDetails,
            CreatedAt = utcNow
        };
        IList<ReviewFlag> reviewFlags = null;
        if (hasReviewFlags)
        {
            reviewFlags = allFlags.Select(f => new ReviewFlag
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
            metrics.RecordDocumentCompleted("extraction");
        else if (nextStatus == DocumentStatus.ReviewRequired)
            metrics.RecordDocumentCompleted("extraction_review_required");
        EnqueueWebhookIfNeeded(doc, logger);
        logger.LogInformation(
            "ExtractionWorker: document {DocumentId} extracted -> {NextStatus} " +
            "(schema={SchemaVersion}, flags={FlagCount})",
            doc.Id, nextStatus, result.SchemaVersion, result.ReviewFlags?.Count ?? 0);
        try
        {
            doc.CanonicalOutput = canonicalOutput;
            await vatValidationService.ValidateDocumentAsync(doc, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ExtractionWorker: VAT validation failed for document {DocumentId}, continuing",
                doc.Id);
        }
    }
    private static async Task ProcessDocumentMultiModelAsync(
        Document doc,
        MultiModelExtractionService multiModelService,
        IStorageService storageService,
        DocumentStatusWorkflow workflow,
        VatValidationService vatValidationService,
        ConspectareMetrics metrics,
        ILogger logger,
        CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        ConsensusResult consensus;
        byte[] rawFileBytes;
        try
        {
            await using var rawFileStream = await storageService.DownloadAsync(doc.RawFileS3Key, ct);
            using var ms = new MemoryStream();
            await rawFileStream.CopyToAsync(ms, ct);
            rawFileBytes = ms.ToArray();
            consensus = await multiModelService.ExtractAsync(doc, rawFileBytes, ct);
        }
        catch (Exception ex)
        {
            HandleExtractionError(doc, workflow, metrics, ex, utcNow, logger);
            return;
        }
        var winningResult = consensus.WinningResult;
        var multiValidationFindings = RunPostExtractionValidation(winningResult.OutputJson, logger);
        var multiAllFlags = new List<ReviewFlagInfo>();
        if (winningResult.ReviewFlags is { Count: > 0 })
            multiAllFlags.AddRange(winningResult.ReviewFlags);
        multiAllFlags.AddRange(multiValidationFindings);
        var hasReviewFlags = multiAllFlags.Count > 0;
        var nextStatus = hasReviewFlags
            ? DocumentStatus.ReviewRequired
            : DocumentStatus.Completed;
        if (!workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
        {
            logger.LogError(
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
        TryDenormalizeFields(canonicalOutput, winningResult.OutputJson);
        var attempts = new List<ExtractionAttempt>();
        var artifacts = new List<DocumentArtifact>();
        foreach (var (providerKey, result) in consensus.AllResults)
        {
            var artifactS3Key = $"tenants/{doc.TenantId}/documents/{doc.Id}/llm_extraction_response_{providerKey}.json";
            var responseBytes = Encoding.UTF8.GetBytes(result.OutputJson);
            using var responseStream = new MemoryStream(responseBytes);
            await storageService.UploadAsync(artifactS3Key, responseStream, "application/json", ct);
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
                Phase = "extraction",
                ModelId = result.ModelId,
                PromptVersion = result.PromptVersion,
                ProviderKey = providerKey,
                Status = providerKey == consensus.WinningProviderKey ? "completed" : "completed_non_winner",
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                LatencyMs = result.LatencyMs,
                CreatedAt = utcNow,
                CompletedAt = utcNow
            });
        }
        var statusDetails = hasReviewFlags
            ? $"Multi-model extraction completed with {multiAllFlags.Count} review flag(s) (strategy={consensus.StrategyUsed}, winner={consensus.WinningProviderKey})"
            : $"Multi-model extraction completed successfully (strategy={consensus.StrategyUsed}, winner={consensus.WinningProviderKey})";
        var statusEvent = new DocumentEvent
        {
            TenantId = doc.TenantId,
            EventType = "status_change",
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = statusDetails,
            CreatedAt = utcNow
        };
        IList<ReviewFlag> reviewFlags = null;
        if (hasReviewFlags)
        {
            reviewFlags = multiAllFlags.Select(f => new ReviewFlag
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
            metrics.RecordDocumentCompleted("extraction");
        else if (nextStatus == DocumentStatus.ReviewRequired)
            metrics.RecordDocumentCompleted("extraction_review_required");
        EnqueueWebhookIfNeeded(doc, logger);
        logger.LogInformation(
            "ExtractionWorker: document {DocumentId} multi-model extracted -> {NextStatus} " +
            "(strategy={Strategy}, winner={Winner}, providers={ProviderCount})",
            doc.Id, nextStatus, consensus.StrategyUsed, consensus.WinningProviderKey, consensus.AllResults.Count);
        try
        {
            doc.CanonicalOutput = canonicalOutput;
            await vatValidationService.ValidateDocumentAsync(doc, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ExtractionWorker: VAT validation failed for document {DocumentId}, continuing",
                doc.Id);
        }
    }
    private static void HandleExtractionError(
        Document doc,
        DocumentStatusWorkflow workflow,
        ConspectareMetrics metrics,
        Exception ex,
        DateTime utcNow,
        ILogger logger)
    {
        doc.RetryCount++;
        doc.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
        doc.UpdatedAt = utcNow;
        var nextStatus = doc.RetryCount >= doc.MaxRetries
            ? DocumentStatus.Failed
            : DocumentStatus.ExtractionFailed;
        if (nextStatus == DocumentStatus.Failed)
            metrics.RecordDocumentFailed("extraction", "max_retries_exceeded");
        if (!workflow.CanTransition(DocumentStatus.Extracting, nextStatus))
        {
            logger.LogError(
                "ExtractionWorker: invalid error transition from {From} to {To} for document {DocumentId}",
                DocumentStatus.Extracting, nextStatus, doc.Id);
            return;
        }
        doc.Status = nextStatus;
        var attempt = new ExtractionAttempt
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            AttemptNumber = doc.RetryCount + 1,
            Phase = "extraction",
            ModelId = "unknown",
            PromptVersion = "unknown",
            Status = "failed",
            ErrorMessage = doc.ErrorMessage,
            CreatedAt = utcNow,
            CompletedAt = utcNow
        };
        var statusEvent = new DocumentEvent
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            EventType = "status_change",
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = $"Extraction failed (attempt {doc.RetryCount}/{doc.MaxRetries}): {doc.ErrorMessage}",
            CreatedAt = utcNow
        };
        new SaveTriageResultCommand(doc, attempt, statusEvent).Execute();
        if (nextStatus == DocumentStatus.Failed)
            EnqueueWebhookIfNeeded(doc, logger);
        logger.LogWarning(ex,
            "ExtractionWorker: document {DocumentId} extraction failed -> {NextStatus} " +
            "(retry {RetryCount}/{MaxRetries})",
            doc.Id, nextStatus, doc.RetryCount, doc.MaxRetries);
    }
    private static void EnqueueWebhookIfNeeded(Document doc, ILogger logger)
    {
        try
        {
            var client = new LoadApiClientByIdQuery(doc.TenantId).Execute();
            WebhookEnqueuer.EnqueueIfNeeded(doc, client, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "ExtractionWorker: failed to enqueue webhook for document {DocumentId}", doc.Id);
        }
    }
    private static List<ReviewFlagInfo> RunPostExtractionValidation(string outputJson, ILogger logger)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
            var invoice = JsonSerializer.Deserialize<CanonicalInvoice>(outputJson, options);
            if (invoice == null) return new List<ReviewFlagInfo>();
            return ExtractionValidator.Validate(invoice);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ExtractionWorker: post-extraction validation failed, skipping");
            return new List<ReviewFlagInfo>();
        }
    }
    private static void TryDenormalizeFields(CanonicalOutput output, string outputJson)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(outputJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("invoice_number", out var inv))
                output.InvoiceNumber = inv.GetString();
            if (root.TryGetProperty("issue_date", out var issueDate) &&
                DateTime.TryParse(issueDate.GetString(), out var parsedIssueDate))
                output.IssueDate = parsedIssueDate;
            if (root.TryGetProperty("due_date", out var dueDate) &&
                DateTime.TryParse(dueDate.GetString(), out var parsedDueDate))
                output.DueDate = parsedDueDate;
            if (root.TryGetProperty("supplier_cui", out var sCui))
                output.SupplierCui = sCui.GetString();
            if (root.TryGetProperty("customer_cui", out var cCui))
                output.CustomerCui = cCui.GetString();
            if (root.TryGetProperty("currency", out var currency))
                output.Currency = currency.GetString();
            if (root.TryGetProperty("total_amount", out var totalAmount) &&
                totalAmount.TryGetDecimal(out var totalAmountVal))
                output.TaxInclusiveAmount = totalAmountVal;
            if (root.TryGetProperty("vat_amount", out var vat) &&
                vat.TryGetDecimal(out var vatVal))
                output.VatAmount = vatVal;
            if (root.TryGetProperty("totals", out var totals))
            {
                if (totals.TryGetProperty("tax_exclusive_amount", out var taxExcl) &&
                    taxExcl.TryGetDecimal(out var taxExclVal))
                    output.TaxExclusiveAmount = taxExclVal;
                if (totals.TryGetProperty("vat_amount", out var totalsVat) &&
                    totalsVat.TryGetDecimal(out var totalsVatVal))
                    output.VatAmount = totalsVatVal;
                if (totals.TryGetProperty("tax_inclusive_amount", out var taxIncl) &&
                    taxIncl.TryGetDecimal(out var taxInclVal))
                    output.TaxInclusiveAmount = taxInclVal;
            }
            if (root.TryGetProperty("discount", out var discount) &&
                discount.TryGetDecimal(out var discountVal))
                output.Discount = discountVal;
            if (root.TryGetProperty("tax_note", out var taxNote))
                output.TaxNote = taxNote.GetString();
            if (root.TryGetProperty("tax_category", out var taxCategory))
                output.TaxCategory = taxCategory.GetString();
            if (root.TryGetProperty("swift_bic", out var swiftBic))
                output.SwiftBic = swiftBic.GetString();
        }
        catch
        {
        }
    }
}
