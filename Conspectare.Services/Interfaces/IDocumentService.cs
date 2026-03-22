using Conspectare.Domain.Entities;
using Conspectare.Services.Models;

namespace Conspectare.Services.Interfaces;

public interface IDocumentService
{
    Task<OperationResult<Document>> IngestAsync(Stream file, string fileName, string contentType, string externalRef, string clientReference, string metadata, string fiscalCode = null, CancellationToken ct = default);
    Task<OperationResult<Document>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<OperationResult<PagedResult<Document>>> ListAsync(string status, string search, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize, CancellationToken ct = default);
    Task<OperationResult<Document>> RetryAsync(long id, CancellationToken ct = default);
    Task<OperationResult<Document>> ResolveAsync(long id, string action, string canonicalOutputJson, CancellationToken ct = default);
}
