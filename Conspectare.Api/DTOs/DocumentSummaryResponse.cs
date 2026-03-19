using Conspectare.Domain.Entities;
using Conspectare.Services;

namespace Conspectare.Api.DTOs;

public record DocumentSummaryResponse(
    long Id,
    string ExternalRef,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string InputFormat,
    string Status,
    string PipelineStatus,
    string DocumentType,
    int RetryCount,
    string ClientReference,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt)
{
    public static DocumentSummaryResponse FromEntity(Document document, DocumentStatusWorkflow workflow)
    {
        var externalStatus = workflow.GetExternalStatus(document.Status);

        return new DocumentSummaryResponse(
            document.Id,
            document.ExternalRef,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.InputFormat,
            externalStatus,
            document.Status,
            document.DocumentType,
            document.RetryCount,
            document.ClientReference,
            document.CreatedAt,
            document.UpdatedAt,
            document.CompletedAt);
    }
}
