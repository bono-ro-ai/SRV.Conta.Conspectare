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

    [HttpPost]
    [Consumes("multipart/form-data")]
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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _documentService.ListAsync(status, page, pageSize, ct);

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

        return File(stream, document.ContentType, document.FileName);
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
