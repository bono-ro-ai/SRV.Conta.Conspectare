using Conspectare.Domain.Entities;

namespace Conspectare.Services.Interfaces;

public interface IReviewService
{
    Task<OperationResult<Document>> ApproveAsync(long tenantId, long documentId, string notes, CancellationToken ct = default);
    Task<OperationResult<Document>> RejectAsync(long tenantId, long documentId, string reason, CancellationToken ct = default);
}
