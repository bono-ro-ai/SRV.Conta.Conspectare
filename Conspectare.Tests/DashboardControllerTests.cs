using Conspectare.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Conspectare.Tests;

public class DashboardControllerTests
{
    private readonly DashboardController _controller;

    public DashboardControllerTests()
    {
        var tenantContext = new MockTenantContext { TenantId = 1 };
        _controller = new DashboardController(tenantContext);
    }

    [Fact]
    public void GetProcessingTimes_FromAfterTo_ReturnsBadRequest()
    {
        var from = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _controller.GetProcessingTimes(from, to);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("'from' must be earlier than 'to'", problem.Detail);
    }

    [Fact]
    public void GetErrorRates_FromAfterTo_ReturnsBadRequest()
    {
        var from = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _controller.GetErrorRates(from, to);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public void GetLlmCosts_FromAfterTo_ReturnsBadRequest()
    {
        var from = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _controller.GetLlmCosts(from, to);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public void GetVolumes_FromAfterTo_ReturnsBadRequest()
    {
        var from = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = _controller.GetVolumes(from, to);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public void GetProcessingTimes_FromEqualsTo_ReturnsBadRequest()
    {
        var date = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        var result = _controller.GetProcessingTimes(date, date);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }
}
