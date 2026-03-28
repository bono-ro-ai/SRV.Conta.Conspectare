using Conspectare.Api.DTOs;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/api-clients")]
public class AdminApiClientsController : ControllerBase
{
    private readonly ITenantContext _tenant;
    private readonly ILogger<AdminApiClientsController> _logger;

    public AdminApiClientsController(ITenantContext tenant, ILogger<AdminApiClientsController> logger)
    {
        _tenant = tenant;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new API client with the given name, rate limit, file size cap, and optional webhook URL.
    /// Returns the generated plain-text API key once; it cannot be retrieved again.
    /// Requires admin access.
    /// </summary>
    [HttpPost]
    public IActionResult Create([FromBody] CreateApiClientRequest request)
    {
        if (!_tenant.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        if (request == null || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Name is required."
            });

        if (request.Name.Length > 200)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Name must not exceed 200 characters."
            });

        if (request.RateLimitPerMin <= 0)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "RateLimitPerMin must be a positive integer."
            });

        if (request.MaxFileSizeMb <= 0)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "MaxFileSizeMb must be a positive integer."
            });

        // Only validate the webhook URL when one is actually supplied.
        if (!string.IsNullOrWhiteSpace(request.WebhookUrl)
            && !(Uri.TryCreate(request.WebhookUrl, UriKind.Absolute, out var uri)
                 && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "WebhookUrl must be a valid absolute HTTP or HTTPS URI."
            });

        var result = new SaveApiClientCommand(
            request.Name,
            request.RateLimitPerMin,
            request.MaxFileSizeMb,
            request.WebhookUrl).Execute();

        _logger.LogInformation("Admin API client created: {ClientId} {ClientName} by admin {AdminPrefix}",
            result.ApiClient.Id, result.ApiClient.Name, _tenant.ApiKeyPrefix);

        var response = new CreateApiClientResponse(
            result.ApiClient.Id,
            result.ApiClient.Name,
            result.ApiClient.ApiKeyPrefix,
            result.PlainKey,
            result.ApiClient.CreatedAt);

        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

    /// <summary>
    /// Returns all API clients registered in the system.
    /// Requires admin access.
    /// </summary>
    [HttpGet]
    public IActionResult List()
    {
        if (!_tenant.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        var clients = new FindAllApiClientsQuery().Execute();
        var items = clients
            .Select(c => new ApiClientListItem(
                c.Id, c.Name, c.ApiKeyPrefix, c.IsActive, c.IsAdmin,
                c.RateLimitPerMin, c.MaxFileSizeMb, c.WebhookUrl, c.CreatedAt))
            .ToList()
            .AsReadOnly();

        return Ok(items);
    }

    /// <summary>
    /// Soft-deletes the API client with the given ID, preventing it from authenticating further.
    /// Requires admin access.
    /// </summary>
    [HttpDelete("{id:long}")]
    public IActionResult SoftDelete(long id)
    {
        if (!_tenant.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });

        var found = new SoftDeleteApiClientCommand(id).Execute();
        if (!found)
            return NotFound(new ProblemDetails
            {
                Type = "https://httpstatuses.com/404",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = $"API client with id {id} not found."
            });

        _logger.LogInformation("Admin API client soft-deleted: {ClientId} by admin {AdminPrefix}",
            id, _tenant.ApiKeyPrefix);

        return NoContent();
    }
}
