namespace Conspectare.Api.DTOs;

public record BatchUploadItemResult(
    int Index,
    string FileName,
    long? Id,
    string DocumentRef,
    string Status,
    string Error,
    int StatusCode);

public record BatchUploadResponse(
    IReadOnlyList<BatchUploadItemResult> Results,
    int TotalFiles,
    int Succeeded,
    int Failed);
