using Conspectare.Api.DTOs;
using Conspectare.Api.Extensions;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/admin/review-queue")]
public class ReviewQueueController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IStorageService _storageService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ReviewQueueController> _logger;

    public ReviewQueueController(
        IReviewService reviewService,
        IStorageService storageService,
        ITenantContext tenantContext,
        ILogger<ReviewQueueController> logger)
    {
        _reviewService = reviewService;
        _storageService = storageService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!_tenantContext.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        if (page < 1)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Page must be >= 1."
            });

        if (pageSize < 1 || pageSize > 200)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "PageSize must be between 1 and 200."
            });

        var tenantId = _tenantContext.TenantId;
        var result = new FindReviewQueueDocumentsQuery(tenantId, page, pageSize).Execute();

        var items = result.Items
            .Select(ReviewQueueItemResponse.FromEntity)
            .ToList()
            .AsReadOnly();

        var response = new ReviewQueueListResponse(
            items,
            result.TotalCount,
            (page * pageSize) < result.TotalCount,
            page,
            pageSize);

        return Ok(response);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        if (!_tenantContext.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        var tenantId = _tenantContext.TenantId;
        var document = new LoadDocumentByIdQuery(tenantId, id).Execute();

        if (document == null)
            return NotFound(new ProblemDetails
            {
                Type = "https://httpstatuses.com/404",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"Document with id {id} not found."
            });

        var preSignedUrl = await _storageService.GeneratePresignedUrlAsync(
            document.RawFileS3Key, TimeSpan.FromMinutes(15), ct);

        return Ok(ReviewQueueDetailResponse.FromEntity(document, preSignedUrl));
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(
        long id,
        [FromBody] ApproveDocumentRequest request,
        CancellationToken ct)
    {
        if (!_tenantContext.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        var tenantId = _tenantContext.TenantId;
        var result = await _reviewService.ApproveAsync(tenantId, id, request?.Notes, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(ReviewQueueItemResponse.FromEntity(result.Data));
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(
        long id,
        [FromBody] RejectDocumentRequest request,
        CancellationToken ct)
    {
        if (!_tenantContext.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Reason is required."
            });

        var tenantId = _tenantContext.TenantId;
        var result = await _reviewService.RejectAsync(tenantId, id, request.Reason, ct);

        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(ReviewQueueItemResponse.FromEntity(result.Data));
    }
}
