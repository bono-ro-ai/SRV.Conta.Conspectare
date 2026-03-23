using System.Security.Cryptography;
using System.Text.Json;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Infrastructure;
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
    private readonly ICanonicalOutputJsonService _canonicalOutputJsonService;
    private readonly ITenantContext _tenantContext;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly IPipelineSignal _pipelineSignal;
    private readonly IDocumentRefAllocator _documentRefAllocator;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ISessionFactory sessionFactory,
        IStorageService storageService,
        ICanonicalOutputJsonService canonicalOutputJsonService,
        ITenantContext tenantContext,
        DocumentStatusWorkflow workflow,
        IPipelineSignal pipelineSignal,
        IDocumentRefAllocator documentRefAllocator,
        ILogger<DocumentService> logger)
    {
        _sessionFactory = sessionFactory;
        _storageService = storageService;
        _canonicalOutputJsonService = canonicalOutputJsonService;
        _tenantContext = tenantContext;
        _workflow = workflow;
        _pipelineSignal = pipelineSignal;
        _documentRefAllocator = documentRefAllocator;
        _logger = logger;
    }

    public async Task<OperationResult<Document>> IngestAsync(
        Stream file,
        string fileName,
        string contentType,
        string externalRef,
        string clientReference,
        string metadata,
        string fiscalCode = null,
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
        var s3Key = S3KeyBuilder.Input(tenantId, fileName);
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
            UploadedBy = _tenantContext.UserIdentity,
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
            EventType = DocumentEventType.Ingested,
            FromStatus = null,
            ToStatus = DocumentStatus.Ingested,
            Details = $"File '{fileName}' ingested, format: {inputFormat}",
            CreatedAt = utcNow
        };

        var triageEvent = new DocumentEvent
        {
            TenantId = tenantId,
            EventType = DocumentEventType.StatusChange,
            FromStatus = DocumentStatus.Ingested,
            ToStatus = DocumentStatus.PendingTriage,
            Details = "Auto-transitioned to pending_triage after ingestion",
            CreatedAt = utcNow
        };

        using var session = _sessionFactory.OpenSession();
        using var transaction = session.BeginTransaction();

        var normalizedFiscalCode = DocumentRefAllocator.NormalizeFiscalCode(fiscalCode);
        var documentRef = await _documentRefAllocator.AllocateRefAsync(session, normalizedFiscalCode);
        document.DocumentRef = documentRef;
        document.FiscalCode = normalizedFiscalCode;

        new SaveIngestedDocumentCommand(document, artifact, ingestedEvent, triageEvent)
            .UseExternalSession(session)
            .Execute();

        await transaction.CommitAsync(ct);

        _pipelineSignal.Signal(PipelinePhase.Triage);

        _logger.LogInformation("Ingested document {DocumentId} for tenant {TenantId}, file '{FileName}'",
            document.Id, tenantId, fileName);

        return OperationResult<Document>.Accepted(document);
    }

    public async Task<OperationResult<Document>> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;

        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, id)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {id} not found.");

        await HydrateCanonicalOutputJsonAsync(document, ct);

        return OperationResult<Document>.Success(document);
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
            EventType = DocumentEventType.StatusChange,
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

        _pipelineSignal.Signal(PipelinePhase.Triage);

        _logger.LogInformation("Retried document {DocumentId} for tenant {TenantId}, retry count: {RetryCount}",
            document.Id, tenantId, document.RetryCount);

        return OperationResult<Document>.Success(document);
    }

    private static readonly HashSet<string> ValidResolveActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirm", "provide_corrected", "reject"
    };

    public async Task<OperationResult<Document>> ResolveAsync(long id, string action, string canonicalOutputJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(action) || !ValidResolveActions.Contains(action))
            return OperationResult<Document>.BadRequest("Action must be one of: confirm, provide_corrected, reject.");

        if (string.Equals(action, "provide_corrected", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(canonicalOutputJson))
            return OperationResult<Document>.BadRequest("canonicalOutputJson is required when action is 'provide_corrected'.");

        var tenantId = _tenantContext.TenantId;

        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, id)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {id} not found.");

        if (document.Status != DocumentStatus.ReviewRequired)
            return OperationResult<Document>.Conflict(
                $"Cannot resolve document in status '{document.Status}'. Only documents in 'review_required' status can be resolved.");

        var targetStatus = string.Equals(action, "reject", StringComparison.OrdinalIgnoreCase)
            ? DocumentStatus.Rejected
            : DocumentStatus.Completed;

        string outputJsonS3Key = null;
        if (string.Equals(action, "provide_corrected", StringComparison.OrdinalIgnoreCase))
        {
            outputJsonS3Key = await _canonicalOutputJsonService.UploadAsync(tenantId, document.Id, canonicalOutputJson, ct);
        }

        var resolvedEvent = new DocumentEvent
        {
            TenantId = tenantId,
            EventType = DocumentEventType.Resolved,
            FromStatus = DocumentStatus.ReviewRequired,
            ToStatus = targetStatus,
            Details = $"Manual resolution: {action}",
            CreatedAt = DateTime.UtcNow
        };

        using var transaction = session.BeginTransaction();

        new ResolveDocumentCommand(document, action, canonicalOutputJson, outputJsonS3Key, resolvedEvent)
            .UseExternalSession(session)
            .Execute();

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Resolved document {DocumentId} for tenant {TenantId} with action '{Action}'",
            document.Id, tenantId, action);

        return OperationResult<Document>.Success(document);
    }

    public async Task<OperationResult<Document>> UpdateCanonicalOutputAsync(long id, string canonicalOutputJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(canonicalOutputJson))
            return OperationResult<Document>.BadRequest("canonicalOutputJson is required.");

        try
        {
            using var parsed = JsonDocument.Parse(canonicalOutputJson);
        }
        catch (JsonException)
        {
            return OperationResult<Document>.BadRequest("canonicalOutputJson must be valid JSON.");
        }

        var tenantId = _tenantContext.TenantId;

        using var session = _sessionFactory.OpenSession();
        var document = new LoadDocumentByIdQuery(tenantId, id)
            .UseExternalSession(session)
            .Execute();

        if (document == null)
            return OperationResult<Document>.NotFound($"Document with id {id} not found.");

        if (document.Status != DocumentStatus.ReviewRequired)
            return OperationResult<Document>.Conflict(
                $"Cannot edit canonical output for document in status '{document.Status}'. Only documents in 'review_required' status can be edited.");

        if (document.CanonicalOutput == null)
            return OperationResult<Document>.Conflict("Document has no canonical output to edit.");

        var utcNow = DateTime.UtcNow;

        var outputJsonS3Key = await _canonicalOutputJsonService.UploadAsync(tenantId, document.Id, canonicalOutputJson, ct);

        using var transaction = session.BeginTransaction();

        new UpdateCanonicalOutputCommand(document, canonicalOutputJson, outputJsonS3Key, utcNow)
            .UseExternalSession(session)
            .Execute();

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Updated canonical output for document {DocumentId} for tenant {TenantId}",
            document.Id, tenantId);

        return OperationResult<Document>.Success(document);
    }

    private async Task HydrateCanonicalOutputJsonAsync(Document document, CancellationToken ct)
    {
        if (document.CanonicalOutput == null)
            return;

        if (!string.IsNullOrEmpty(document.CanonicalOutput.OutputJson))
            return;

        if (!string.IsNullOrEmpty(document.CanonicalOutput.OutputJsonS3Key))
        {
            document.CanonicalOutput.OutputJson =
                await _canonicalOutputJsonService.DownloadAsync(document.CanonicalOutput.OutputJsonS3Key, ct);
            return;
        }

        // Fallback: read legacy output_json from DB for pre-migration documents
        using var fallbackSession = _sessionFactory.OpenSession();
        var legacyJson = fallbackSession.CreateSQLQuery(
                "SELECT output_json FROM pipe_canonical_outputs WHERE id = :id")
            .SetParameter("id", document.CanonicalOutput.Id)
            .UniqueResult<string>();

        if (!string.IsNullOrEmpty(legacyJson))
            document.CanonicalOutput.OutputJson = legacyJson;
    }
}
