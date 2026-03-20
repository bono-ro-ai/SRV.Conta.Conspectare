using System.Security.Cryptography;
using System.Text;
using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/admin/api-clients")]
public class AdminApiClientsController : ControllerBase
{
    private readonly ITenantContext _tenant;

    public AdminApiClientsController(ITenantContext tenant)
    {
        _tenant = tenant;
    }

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
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";
        var prefix = plainKey[..8];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        var hashHex = Convert.ToHexStringLower(hash);
        var now = DateTime.UtcNow;
        var apiClient = new ApiClient
        {
            Name = request.Name,
            ApiKeyHash = hashHex,
            ApiKeyPrefix = prefix,
            IsActive = true,
            IsAdmin = false,
            RateLimitPerMin = request.RateLimitPerMin > 0 ? request.RateLimitPerMin : 60,
            MaxFileSizeMb = request.MaxFileSizeMb > 0 ? request.MaxFileSizeMb : 10,
            WebhookUrl = request.WebhookUrl,
            CreatedAt = now,
            UpdatedAt = now
        };
        new SaveApiClientCommand(apiClient).Execute();
        var response = new CreateApiClientResponse(
            apiClient.Id,
            apiClient.Name,
            apiClient.ApiKeyPrefix,
            plainKey,
            apiClient.CreatedAt);
        return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
    }

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
        return NoContent();
    }
}
