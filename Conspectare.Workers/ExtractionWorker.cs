using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class ExtractionWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;
    protected override string JobName => "extraction_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    public ExtractionWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExtractionWorker>>();
        var processorRegistry = scope.ServiceProvider.GetRequiredService<IProcessorRegistry>();
        var storageService = scope.ServiceProvider.GetRequiredService<IStorageService>();
        var workflow = scope.ServiceProvider.GetRequiredService<DocumentStatusWorkflow>();
        var pendingDocs = new FindPendingExtractionDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;
        var claimedDocs = new ClaimDocumentsForExtractionCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;
        var processedCount = 0;
        foreach (var doc in claimedDocs)
        {
            try
            {
                await ProcessDocumentAsync(doc, processorRegistry, storageService, workflow, logger, ct);
                processedCount++;
            }
            catch (Exception ex)
            {
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
            HandleExtractionError(doc, workflow, ex, utcNow, logger);
            return;
        }
        var hasReviewFlags = result.ReviewFlags is { Count: > 0 };
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
            ? $"Extraction completed with {result.ReviewFlags!.Count} review flag(s)"
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
        logger.LogInformation(
            "ExtractionWorker: document {DocumentId} extracted -> {NextStatus} " +
            "(schema={SchemaVersion}, flags={FlagCount})",
            doc.Id, nextStatus, result.SchemaVersion, result.ReviewFlags?.Count ?? 0);
    }
    private static void HandleExtractionError(
        Document doc,
        DocumentStatusWorkflow workflow,
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
        logger.LogWarning(ex,
            "ExtractionWorker: document {DocumentId} extraction failed -> {NextStatus} " +
            "(retry {RetryCount}/{MaxRetries})",
            doc.Id, nextStatus, doc.RetryCount, doc.MaxRetries);
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
            if (root.TryGetProperty("total_amount", out var total) &&
                total.TryGetDecimal(out var totalVal))
                output.TotalAmount = totalVal;
            if (root.TryGetProperty("vat_amount", out var vat) &&
                vat.TryGetDecimal(out var vatVal))
                output.VatAmount = vatVal;
        }
        catch
        {
        }
    }
}
