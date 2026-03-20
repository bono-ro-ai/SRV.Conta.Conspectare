using Conspectare.Domain.Entities;

namespace Conspectare.Api.DTOs;

public record ReviewQueueItemResponse(
    long Id,
    string ExternalRef,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string DocumentType,
    decimal? TriageConfidence,
    int ReviewFlagCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ReviewQueueItemResponse FromEntity(Document document) =>
        new(
            document.Id,
            document.ExternalRef,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.DocumentType,
            document.TriageConfidence,
            document.ReviewFlags?.Count ?? 0,
            document.CreatedAt,
            document.UpdatedAt);
}

public record ReviewQueueDetailResponse(
    long Id,
    string ExternalRef,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string DocumentType,
    decimal? TriageConfidence,
    int ReviewFlagCount,
    string CanonicalOutputJson,
    IReadOnlyList<ReviewFlagResponse> ReviewFlags,
    string PreSignedUrl,
    bool? IsAccountingRelevant,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static ReviewQueueDetailResponse FromEntity(Document document, string preSignedUrl)
    {
        var flags = document.ReviewFlags?
            .Select(ReviewFlagResponse.FromEntity)
            .ToList()
            .AsReadOnly()
            ?? (IReadOnlyList<ReviewFlagResponse>)Array.Empty<ReviewFlagResponse>();

        return new ReviewQueueDetailResponse(
            document.Id,
            document.ExternalRef,
            document.FileName,
            document.ContentType,
            document.FileSizeBytes,
            document.DocumentType,
            document.TriageConfidence,
            document.ReviewFlags?.Count ?? 0,
            document.CanonicalOutput?.OutputJson,
            flags,
            preSignedUrl,
            document.IsAccountingRelevant,
            document.CreatedAt,
            document.UpdatedAt);
    }
}

public record ApproveDocumentRequest
{
    public string Notes { get; init; }
}

public record RejectDocumentRequest
{
    public string Reason { get; init; }
}

public record ReviewQueueListResponse(
    IReadOnlyList<ReviewQueueItemResponse> Items,
    int TotalRecords,
    bool HasMoreRecords,
    int Page,
    int PageSize);
