using Conspectare.Api.DTOs;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Conspectare.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ITenantContext _tenant;

    public DashboardController(ITenantContext tenant)
    {
        _tenant = tenant;
    }

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

    [HttpGet("error-rates")]
    public IActionResult GetErrorRates(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var (rangeFrom, rangeTo, error) = ResolveRange(from, to);
        if (error != null) return error;

        var result = new FindErrorRatesQuery(_tenant.TenantId, rangeFrom, rangeTo).Execute();

        var errorRate = result.Total > 0
            ? Math.Round((decimal)result.Failed / result.Total * 100, 2)
            : 0m;

        return Ok(new ErrorRatesResponse(result.Total, result.Failed, errorRate, rangeFrom, rangeTo));
    }

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
