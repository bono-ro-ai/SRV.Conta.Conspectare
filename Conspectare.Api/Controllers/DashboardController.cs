using Conspectare.Api.DTOs;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ITenantContext _tenant;

    public DashboardController(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    /// <summary>
    /// Returns the current document count grouped by processing status for the authenticated tenant.
    /// </summary>
    [HttpGet("queue-depths")]
    public IActionResult GetQueueDepths()
    {
        var results = new FindQueueDepthsQuery(_tenant.TenantId).Execute();

        var items = results
            .Select(r => new QueueDepthItem(r.Status, r.Count))
            .ToList()
            .AsReadOnly();

        var total = items.Sum(i => i.Count);

        return Ok(new QueueDepthsResponse(items, total));
    }

    /// <summary>
    /// Returns P50 and P95 document processing-time percentiles for the given date range.
    /// Defaults to the last 30 days when no range is specified.
    /// </summary>
    [HttpGet("processing-times")]
    public IActionResult GetProcessingTimes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo, error) = ResolveRange(from, to);
        if (error != null) return error;

        var result = new FindProcessingTimesQuery(_tenant.TenantId, rangeFrom, rangeTo).Execute();

        return Ok(new ProcessingTimesResponse(result.P50, result.P95, result.SampleCount, rangeFrom, rangeTo));
    }

    /// <summary>
    /// Returns failed-document counts and the computed error-rate percentage for the given date range.
    /// Defaults to the last 30 days when no range is specified.
    /// </summary>
    [HttpGet("error-rates")]
    public IActionResult GetErrorRates(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo, error) = ResolveRange(from, to);
        if (error != null) return error;

        var result = new FindErrorRatesQuery(_tenant.TenantId, rangeFrom, rangeTo).Execute();

        // Avoid division by zero when no documents have been processed yet.
        var errorRate = result.Total > 0
            ? Math.Round((decimal)result.Failed / result.Total * 100, 2)
            : 0m;

        return Ok(new ErrorRatesResponse(result.Total, result.Failed, errorRate, rangeFrom, rangeTo));
    }

    /// <summary>
    /// Returns token consumption and request counts broken down by LLM model for the given date range.
    /// Defaults to the last 30 days when no range is specified.
    /// </summary>
    [HttpGet("llm-costs")]
    public IActionResult GetLlmCosts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo, error) = ResolveRange(from, to);
        if (error != null) return error;

        var results = new FindLlmCostsQuery(_tenant.TenantId, rangeFrom, rangeTo).Execute();

        var items = results
            .Select(r => new LlmCostItem(r.ModelId, r.TotalInputTokens ?? 0, r.TotalOutputTokens ?? 0, r.AttemptCount))
            .ToList()
            .AsReadOnly();

        var grandInputTokens = items.Sum(i => i.TotalInputTokens);
        var grandOutputTokens = items.Sum(i => i.TotalOutputTokens);

        return Ok(new LlmCostsResponse(items, grandInputTokens, grandOutputTokens, rangeFrom, rangeTo));
    }

    /// <summary>
    /// Returns daily document-ingestion volumes for the given date range.
    /// Defaults to the last 30 days when no range is specified.
    /// </summary>
    [HttpGet("volumes")]
    public IActionResult GetVolumes(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo, error) = ResolveRange(from, to);
        if (error != null) return error;

        var results = new FindTenantVolumesQuery(_tenant.TenantId, rangeFrom, rangeTo).Execute();

        var items = results
            .Select(r => new VolumeItem(r.Date, r.Count))
            .ToList()
            .AsReadOnly();

        var total = items.Sum(i => i.Count);

        return Ok(new TenantVolumesResponse(items, total, rangeFrom, rangeTo));
    }

    /// <summary>
    /// Resolves optional from/to query parameters, applying a 30-day default window and
    /// validating that <paramref name="from"/> is strictly earlier than <paramref name="to"/>.
    /// Returns a 400 error result in the third tuple element when validation fails.
    /// </summary>
    private (DateTime from, DateTime to, IActionResult error) ResolveRange(DateTime? from, DateTime? to)
    {
        var rangeFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var rangeTo = to ?? DateTime.UtcNow;

        if (rangeFrom >= rangeTo)
        {
            var problem = new ProblemDetails
            {
                Type = "https://httpstatuses.com/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "'from' must be earlier than 'to'."
            };
            return (default, default, BadRequest(problem));
        }

        return (rangeFrom, rangeTo, null);
    }
}
