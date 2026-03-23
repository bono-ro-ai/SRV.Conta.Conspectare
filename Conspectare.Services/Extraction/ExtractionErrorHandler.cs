using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Logging;
namespace Conspectare.Services.Extraction;
public static class ExtractionErrorHandler
{
    public static void Handle(
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
            metrics.RecordDocumentFailed(PipelinePhase.Extraction, "max_retries_exceeded");
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
            Phase = PipelinePhase.Extraction,
            ModelId = "unknown",
            PromptVersion = "unknown",
            Status = ExtractionAttemptStatus.Failed,
            ErrorMessage = doc.ErrorMessage,
            CreatedAt = utcNow,
            CompletedAt = utcNow
        };
        var statusEvent = new DocumentEvent
        {
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = DocumentStatus.Extracting,
            ToStatus = nextStatus,
            Details = $"Extraction failed (attempt {doc.RetryCount}/{doc.MaxRetries}): {doc.ErrorMessage}",
            CreatedAt = utcNow
        };
        new SaveTriageResultCommand(doc, attempt, statusEvent).Execute();
        if (nextStatus == DocumentStatus.Failed)
            WebhookNotifier.NotifyIfNeeded(doc, logger);
        logger.LogWarning(ex,
            "ExtractionWorker: document {DocumentId} extraction failed -> {NextStatus} " +
            "(retry {RetryCount}/{MaxRetries})",
            doc.Id, nextStatus, doc.RetryCount, doc.MaxRetries);
    }
}
