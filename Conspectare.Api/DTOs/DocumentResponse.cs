using Conspectare.Domain.Entities;
using Conspectare.Services;

namespace Conspectare.Api.DTOs;

public record DocumentResponse(
    long Id,
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
    string CanonicalOutputJson,
    IReadOnlyList<ReviewFlagResponse> ReviewFlags,
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

        var canonicalJson = document.CanonicalOutput?.OutputJson;

        return new DocumentResponse(
            document.Id,
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
            canonicalJson,
            flags,
            document.CreatedAt,
            document.UpdatedAt,
            document.CompletedAt);
    }
}
