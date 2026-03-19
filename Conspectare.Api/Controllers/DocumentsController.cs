using Conspectare.Api.DTOs;
using Conspectare.Api.Extensions;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IStorageService _storageService;
    private readonly DocumentStatusWorkflow _workflow;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        IStorageService storageService,
        DocumentStatusWorkflow workflow,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _storageService = storageService;
        _workflow = workflow;
        _logger = logger;
    }

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/xml", "application/xml",
        "application/pdf",
        "image/jpeg", "image/png", "image/tiff", "image/heic", "image/webp",
        "application/json",
        "text/csv",
        "application/octet-stream"
    };

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromHeader(Name = "X-Request-Id")] string externalRef,
        [FromForm] string clientReference,
        [FromForm] string metadata,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "A non-empty file is required."
            });

        if (!AllowedContentTypes.Contains(file.ContentType))
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"Content type '{file.ContentType}' is not supported."
            });

        using var stream = file.OpenReadStream();
        var result = await _documentService.IngestAsync(
            stream,
            file.FileName,
            file.ContentType,
            externalRef,
            clientReference,
            metadata,
            ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var response = new UploadAcceptedResponse(
            result.Data.Id,
            _workflow.GetExternalStatus(result.Data.Status),
            result.Data.CreatedAt);

        return new ObjectResult(response) { StatusCode = result.StatusCode };
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var result = await _documentService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(DocumentResponse.FromEntity(result.Data, _workflow));
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string status,
        [FromQuery] string search,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _documentService.ListAsync(status, search, dateFrom, dateTo, page, pageSize, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var items = result.Data.Items
            .Select(d => DocumentSummaryResponse.FromEntity(d, _workflow))
            .ToList()
            .AsReadOnly();

        var response = new DocumentListResponse(items, result.Data.TotalCount, result.Data.Page, result.Data.PageSize);
        return Ok(response);
    }

    [HttpGet("{id:long}/raw")]
    public async Task<IActionResult> DownloadRaw(long id, CancellationToken ct)
    {
        var result = await _documentService.GetByIdAsync(id, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var document = result.Data;
        var stream = await _storageService.DownloadAsync(document.RawFileS3Key, ct);

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(stream, "application/octet-stream", document.FileName);
    }

    [HttpPost("{id:long}/retry")]
    public async Task<IActionResult> Retry(long id, CancellationToken ct)
    {
        var result = await _documentService.RetryAsync(id, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(DocumentResponse.FromEntity(result.Data, _workflow));
    }
}
