using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Logging;
using ISessionFactory = NHibernate.ISessionFactory;

namespace Conspectare.Services;

public class ReviewService : IReviewService
{
    private readonly ISessionFactory _sessionFactory;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        ISessionFactory sessionFactory,
        DocumentStatusWorkflow workflow,
        ILogger<ReviewService> logger)
    {
        _sessionFactory = sessionFactory;
        _workflow = workflow;
        _logger = logger;
    }

    public async Task<OperationResult<Document>> ApproveAsync(long tenantId, long documentId, string notes, CancellationToken ct = default)
    {
        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, documentId)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {documentId} not found.");

        if (!_workflow.CanTransition(document.Status, DocumentStatus.Completed))
            return OperationResult<Document>.Conflict(
                $"Cannot approve document in status '{document.Status}'.");

        var previousStatus = document.Status;
        var utcNow = DateTime.UtcNow;

        document.Status = DocumentStatus.Completed;
        document.CompletedAt = utcNow;
        document.UpdatedAt = utcNow;

        var flagsToResolve = document.ReviewFlags?
            .Where(f => !f.IsResolved)
            .ToList() ?? new List<ReviewFlag>();

        foreach (var flag in flagsToResolve)
        {
            flag.IsResolved = true;
            flag.ResolvedAt = utcNow;
        }

        var auditEvent = new DocumentEvent
        {
            Document = document,
            DocumentId = document.Id,
            TenantId = document.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = previousStatus,
            ToStatus = DocumentStatus.Completed,
            Details = string.IsNullOrWhiteSpace(notes) ? "Approved via review queue" : notes,
            CreatedAt = utcNow
        };

        using var tran = session.BeginTransaction();
        new ApproveDocumentCommand(document, flagsToResolve, auditEvent)
            .UseExternalSession(session)
            .Execute();
        await tran.CommitAsync(ct);

        _logger.LogInformation("Approved document {DocumentId} for tenant {TenantId}", documentId, tenantId);

        return OperationResult<Document>.Success(document);
    }

    public async Task<OperationResult<Document>> RejectAsync(long tenantId, long documentId, string reason, CancellationToken ct = default)
    {
        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, documentId)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {documentId} not found.");

        if (!_workflow.CanTransition(document.Status, DocumentStatus.Rejected))
            return OperationResult<Document>.Conflict(
                $"Cannot reject document in status '{document.Status}'.");

        var previousStatus = document.Status;
        var utcNow = DateTime.UtcNow;

        document.Status = DocumentStatus.Rejected;
        document.UpdatedAt = utcNow;

        var auditEvent = new DocumentEvent
        {
            Document = document,
            DocumentId = document.Id,
            TenantId = document.TenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = previousStatus,
            ToStatus = DocumentStatus.Rejected,
            Details = reason,
            CreatedAt = utcNow
        };

        using var tran = session.BeginTransaction();
        new RejectDocumentCommand(document, auditEvent)
            .UseExternalSession(session)
            .Execute();
        await tran.CommitAsync(ct);

        _logger.LogInformation("Rejected document {DocumentId} for tenant {TenantId}, reason: {Reason}", documentId, tenantId, reason);

        return OperationResult<Document>.Success(document);
    }
}
