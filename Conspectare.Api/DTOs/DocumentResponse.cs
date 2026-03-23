using Conspectare.Domain.Entities;
using Conspectare.Services;

namespace Conspectare.Api.DTOs;

public record DocumentResponse(
    long Id,
    string DocumentRef,
    string FiscalCode,
    string ExternalRef,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string InputFormat,
    string Status,
    string DocumentType,
    decimal? TriageConfidence,
    bool? IsAccountingRelevant,
    int RetryCount,
    int MaxRetries,
    string ErrorMessage,
    string ClientReference,
    string Metadata,
    string UploadedBy,
    string CanonicalOutputJson,
    IReadOnlyList<ReviewFlagResponse> ReviewFlags,
    IReadOnlyList<DocumentEventResponse> Events,
    IReadOnlyList<ExtractionAttemptResponse> ExtractionAttempts,
    bool IsTerminal,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt)
{
    public static DocumentResponse FromEntity(Document document, DocumentStatusWorkflow workflow)
    {
        var externalStatus = workflow.GetExternalStatus(document.Status);

        var flags = document.ReviewFlags?
            .Select(ReviewFlagResponse.FromEntity)
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<ReviewFlagResponse>)Array.Empty<ReviewFlagResponse>();

        var events = document.Events?
            .OrderBy(e => e.CreatedAt)
            .Select(DocumentEventResponse.FromEntity)
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<DocumentEventResponse>)Array.Empty<DocumentEventResponse>();

        var attempts = document.ExtractionAttempts?
            .OrderBy(a => a.AttemptNumber)
            .Select(ExtractionAttemptResponse.FromEntity)
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<ExtractionAttemptResponse>)Array.Empty<ExtractionAttemptResponse>();

        var canonicalJson = document.CanonicalOutput?.OutputJson;

        return new DocumentResponse(
            document.Id,
            document.DocumentRef,
            document.FiscalCode,
            document.ExternalRef,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.InputFormat,
            externalStatus,
            document.DocumentType,
            document.TriageConfidence,
            document.IsAccountingRelevant,
            document.RetryCount,
            document.MaxRetries,
            document.ErrorMessage,
            document.ClientReference,
            document.Metadata,
            document.UploadedBy,
            canonicalJson,
            flags,
            events,
            attempts,
            workflow.IsTerminalState(document.Status),
            document.CreatedAt,
            document.UpdatedAt,
            document.CompletedAt);
    }
}
