using Conspectare.Api.DTOs;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/admin/usage")]
public class AdminUsageController : ControllerBase
{
    private readonly ITenantContext _tenant;

    public AdminUsageController(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    [HttpGet]
    public IActionResult GetDailyUsage([FromQuery] long tenantId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_tenant.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });
        if (tenantId <= 0)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "tenantId must be a positive integer."
            });
        if (from == default || to == default)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "from and to query parameters are required."
            });
        if (from > to)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "from must be before or equal to to."
            });

        var rows = new FindUsageDailyQuery(tenantId, from, to).Execute();
        var items = rows.Select(r => new UsageDailyItem(
            r.UsageDate, r.DocumentsIngested, r.DocumentsProcessed,
            r.LlmInputTokens, r.LlmOutputTokens, r.LlmRequests,
            r.StorageBytes, r.ApiCalls)).ToList().AsReadOnly();
        return Ok(new UsageDailyResponse(items, tenantId, from, to));
    }

    [HttpGet("monthly")]
    public IActionResult GetMonthlyUsage([FromQuery] long tenantId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_tenant.IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Type = "https://httpstatuses.com/403",
                Title = "Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Admin access required."
            });
        if (tenantId <= 0)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "tenantId must be a positive integer."
            });
        if (from == default || to == default)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "from and to query parameters are required."
            });
        if (from > to)
            return BadRequest(new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "from must be before or equal to to."
            });

        var summaries = new FindMonthlyUsageSummaryQuery(tenantId, from, to).Execute();
        var items = summaries.Select(s => new MonthlyUsageSummaryItem(
            s.Year, s.Month, s.DocumentsIngested, s.DocumentsProcessed,
            s.LlmInputTokens, s.LlmOutputTokens, s.LlmRequests,
            s.StorageBytes, s.ApiCalls)).ToList().AsReadOnly();
        return Ok(new MonthlyUsageSummaryResponse(items, tenantId, from, to));
    }
}
