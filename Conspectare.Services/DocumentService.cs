using System.Security.Cryptography;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Logging;
using ISession = NHibernate.ISession;
using ISessionFactory = NHibernate.ISessionFactory;

namespace Conspectare.Services;

public class DocumentService : IDocumentService
{
    private readonly ISessionFactory _sessionFactory;
    private readonly IStorageService _storageService;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ISessionFactory sessionFactory,
        IStorageService storageService,
        ITenantContext tenantContext,
        DocumentStatusWorkflow workflow,
        ILogger<DocumentService> logger)
    {
        _sessionFactory = sessionFactory;
        _storageService = storageService;
        _tenantContext = tenantContext;
        _workflow = workflow;
        _logger = logger;
    }

    public async Task<OperationResult<Document>> IngestAsync(
        Stream file,
        string fileName,
        string contentType,
        string externalRef,
        string clientReference,
        string metadata,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return OperationResult<Document>.BadRequest("fileName is required.");

        if (file == null || file.Length == 0)
            return OperationResult<Document>.BadRequest("File stream is required and must not be empty.");

        if (string.IsNullOrWhiteSpace(contentType))
            return OperationResult<Document>.BadRequest("contentType is required.");

        var tenantId = _tenantContext.TenantId;

        // Dedup check by externalRef
        if (!string.IsNullOrWhiteSpace(externalRef))
        {
            using var dedupSession = _sessionFactory.OpenSession();
            var existing = new FindDocumentByExternalRefQuery(tenantId, externalRef)
                .UseExternalSession(dedupSession)
                .Execute();

            if (existing != null)
            {
                _logger.LogInformation("Duplicate externalRef {ExternalRef} for tenant {TenantId}, returning existing document {DocumentId}",
                    externalRef, tenantId, existing.Id);
                return OperationResult<Document>.Success(existing);
            }
        }

        // Detect input format
        var inputFormat = InputFormatDetector.Detect(fileName, contentType);

        // Compute SHA-256 content hash
        string contentHash;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = await sha256.ComputeHashAsync(file, ct);
            contentHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        file.Position = 0;

        // Generate S3 key and upload
        var s3Key = $"{tenantId}/raw/{Guid.NewGuid()}/{fileName}";
        try
        {
            await _storageService.UploadAsync(s3Key, file, contentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to S3 for tenant {TenantId}, key {S3Key}", tenantId, s3Key);
            return OperationResult<Document>.ServerError("Failed to upload file to storage.");
        }

        var utcNow = DateTime.UtcNow;

        var document = new Document
        {
            TenantId = tenantId,
            ExternalRef = externalRef,
            ContentHash = contentHash,
            FileName = fileName,
            ContentType = contentType,
            FileSizeBytes = file.Length,
            InputFormat = inputFormat,
            Status = DocumentStatus.PendingTriage,
            RetryCount = 0,
            MaxRetries = 3,
            RawFileS3Key = s3Key,
            ClientReference = clientReference,
            Metadata = metadata,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var artifact = new DocumentArtifact
        {
            TenantId = tenantId,
            ArtifactType = ArtifactType.Raw,
            S3Key = s3Key,
            ContentType = contentType,
            SizeBytes = file.Length,
            RetentionDays = 365,
            CreatedAt = utcNow
        };

        var ingestedEvent = new DocumentEvent
        {
            TenantId = tenantId,
            EventType = "ingested",
            FromStatus = null,
            ToStatus = DocumentStatus.Ingested,
            Details = $"File '{fileName}' ingested, format: {inputFormat}",
            CreatedAt = utcNow
        };

        var triageEvent = new DocumentEvent
        {
            TenantId = tenantId,
            EventType = "status_change",
            FromStatus = DocumentStatus.Ingested,
            ToStatus = DocumentStatus.PendingTriage,
            Details = "Auto-transitioned to pending_triage after ingestion",
            CreatedAt = utcNow
        };

        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        new SaveIngestedDocumentCommand(document, artifact, ingestedEvent, triageEvent)
            .UseExternalSession(session)
            .Execute();

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Ingested document {DocumentId} for tenant {TenantId}, file '{FileName}'",
            document.Id, tenantId, fileName);

        return OperationResult<Document>.Accepted(document);
    }

    public Task<OperationResult<Document>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;

        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, id)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return Task.FromResult(OperationResult<Document>.NotFound($"Document with id {id} not found."));

        return Task.FromResult(OperationResult<Document>.Success(document));
    }

    public Task<OperationResult<PagedResult<Document>>> ListAsync(
        string status,
        string search,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)
            return Task.FromResult(OperationResult<PagedResult<Document>>.BadRequest("Page must be >= 1."));

        if (pageSize < 1 || pageSize > 100)
            return Task.FromResult(OperationResult<PagedResult<Document>>.BadRequest("PageSize must be between 1 and 100."));

        var tenantId = _tenantContext.TenantId;

        var statuses = string.IsNullOrWhiteSpace(status)
            ? Array.Empty<string>()
            : status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        using var session = _sessionFactory.OpenSession();
        var result = new FindDocumentsPagedQuery(tenantId, statuses, search, dateFrom, dateTo, page, pageSize)
            .UseExternalSession(session)
            .Execute();

        return Task.FromResult(OperationResult<PagedResult<Document>>.Success(result));
    }

    public async Task<OperationResult<Document>> RetryAsync(long id, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;

        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, id)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {id} not found.");

        if (!_workflow.CanTransition(document.Status, DocumentStatus.PendingTriage))
            return OperationResult<Document>.Conflict(
                $"Cannot retry document in status '{document.Status}'.");

        if (document.RetryCount >= document.MaxRetries)
            return OperationResult<Document>.BadRequest(
                $"Maximum retry count ({document.MaxRetries}) exceeded.");

        var retryEvent = new DocumentEvent
        {
            TenantId = tenantId,
            EventType = "status_change",
            FromStatus = document.Status,
            ToStatus = DocumentStatus.PendingTriage,
            Details = $"Manual retry requested (attempt {document.RetryCount + 1})",
            CreatedAt = DateTime.UtcNow
        };

        using var transaction = session.BeginTransaction();

        new ResetDocumentForRetryCommand(document, retryEvent)
            .UseExternalSession(session)
            .Execute();

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Retried document {DocumentId} for tenant {TenantId}, retry count: {RetryCount}",
            document.Id, tenantId, document.RetryCount);

        return OperationResult<Document>.Success(document);
    }
}
