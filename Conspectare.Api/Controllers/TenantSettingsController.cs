using Conspectare.Api.DTOs;
using Conspectare.Api.Extensions;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/tenant/settings")]
[Authorize]
public class TenantSettingsController : ControllerBase
{
    private readonly ITenantSettingsService _settingsService;

    public TenantSettingsController(ITenantSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var result = await _settingsService.GetSettingsAsync();
        if (!result.IsSuccess)
            return result.ToActionResult();

        var s = result.Data;
        return Ok(new TenantSettingsResponse(
            s.TenantId,
            s.CompanyName,
            s.Cui,
            s.ContactEmail,
            s.WebhookUrl,
            s.HasWebhookSecret,
            s.ApiKeyPrefix,
            s.TrialExpiresAt,
            s.IsTrialActive));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateTenantSettingsRequest request)
    {
        if (request == null)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Request body is required."
            });
        }

        var input = new UpdateTenantSettingsInput(
            request.CompanyName,
            request.Cui,
            request.WebhookUrl,
            request.WebhookSecret);

        var result = await _settingsService.UpdateSettingsAsync(input);
        if (!result.IsSuccess)
            return result.ToActionResult();

        var s = result.Data;
        return Ok(new TenantSettingsResponse(
            s.TenantId,
            s.CompanyName,
            s.Cui,
            s.ContactEmail,
            s.WebhookUrl,
            s.HasWebhookSecret,
            s.ApiKeyPrefix,
            s.TrialExpiresAt,
            s.IsTrialActive));
    }

    [HttpPost("rotate-api-key")]
    public async Task<IActionResult> RotateApiKey()
    {
        var result = await _settingsService.RotateApiKeyAsync();
        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(new RotateApiKeyResponse(result.Data.PlainApiKey, result.Data.ApiKeyPrefix));
    }
}
