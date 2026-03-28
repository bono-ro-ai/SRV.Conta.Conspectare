using Conspectare.Api.DTOs;
using Conspectare.Api.Extensions;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/review-queue")]
public class ReviewQueueController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly IStorageService _storageService;
    private readonly ICanonicalOutputJsonService _canonicalOutputJsonService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ReviewQueueController> _logger;

    public ReviewQueueController(
        IReviewService reviewService,
        IStorageService storageService,
        ICanonicalOutputJsonService canonicalOutputJsonService,
        ITenantContext tenantContext,
        ILogger<ReviewQueueController> logger)
    {
        _reviewService = reviewService;
        _storageService = storageService;
        _canonicalOutputJsonService = canonicalOutputJsonService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paginated list of documents currently awaiting human review.
    /// Requires admin access.
    /// </summary>
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
            // HasNextPage: true when the current page does not exhaust the total.
            (page * pageSize) < result.TotalCount,
            page,
            pageSize);

        return Ok(response);
    }

    /// <summary>
    /// Returns the full detail of a single review-queue document, including a 15-minute
    /// pre-signed S3 URL for the raw file and the current canonical output JSON (if present).
    /// Requires admin access.
    /// </summary>
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

        // Only fetch the canonical output JSON when the document has one stored in S3.
        string canonicalOutputJson = null;
        if (document.CanonicalOutput != null && !string.IsNullOrEmpty(document.CanonicalOutput.OutputJsonS3Key))
            canonicalOutputJson = await _canonicalOutputJsonService.DownloadAsync(document.CanonicalOutput.OutputJsonS3Key, ct);

        return Ok(ReviewQueueDetailResponse.FromEntity(document, preSignedUrl, canonicalOutputJson));
    }

    /// <summary>
    /// Approves the specified document, optionally attaching reviewer notes.
    /// Transitions the document out of the review queue.
    /// Requires admin access.
    /// </summary>
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

    /// <summary>
    /// Rejects the specified document with a mandatory reason.
    /// Transitions the document out of the review queue.
    /// Requires admin access.
    /// </summary>
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
