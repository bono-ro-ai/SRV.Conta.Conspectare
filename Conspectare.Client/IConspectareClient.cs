using Conspectare.Client.Models;

namespace Conspectare.Client;

public interface IConspectareClient
{
    Task<UploadAcceptedResponse> SubmitDocumentAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        string? clientReference = null,
        string? fiscalCode = null,
        string? metadata = null,
        CancellationToken ct = default);

    Task<BatchUploadResponse> SubmitBatchAsync(
        IReadOnlyList<BatchFileInput> files,
        string? clientReference = null,
        string? fiscalCode = null,
        string? metadata = null,
        CancellationToken ct = default);

    Task<DocumentResponse> GetDocumentAsync(long documentId, CancellationToken ct = default);

    Task<DocumentListResponse> ListDocumentsAsync(
        string? status = null,
        string? search = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<Stream> DownloadRawAsync(long documentId, CancellationToken ct = default);

    Task<DocumentResponse> RetryDocumentAsync(long documentId, CancellationToken ct = default);

    Task<DocumentResponse> ResolveDocumentAsync(long documentId, ResolveDocumentRequest request, CancellationToken ct = default);

    Task<DocumentResponse> UpdateCanonicalOutputAsync(long documentId, UpdateCanonicalOutputRequest request, CancellationToken ct = default);
}

public class BatchFileInput
{
    public required Stream Stream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}
