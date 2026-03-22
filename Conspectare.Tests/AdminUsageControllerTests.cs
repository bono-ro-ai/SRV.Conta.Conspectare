using Conspectare.Api.Controllers;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Conspectare.Tests;

public class AdminUsageControllerTests
{
    private static AdminUsageController CreateController(bool isAdmin)
    {
        var tenant = new MockTenantContext { TenantId = 1, ApiKeyPrefix = "csp_test", IsAdmin = isAdmin };
        return new AdminUsageController(tenant);
    }

    [Fact]
    public void GetDailyUsage_NonAdmin_Returns403()
    {
        var controller = CreateController(isAdmin: false);
        var result = controller.GetDailyUsage(1, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public void GetDailyUsage_InvalidTenantId_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.GetDailyUsage(0, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("tenantId", problem.Detail);
    }

    [Fact]
    public void GetDailyUsage_FromAfterTo_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.GetDailyUsage(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(-7));
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("from must be before", problem.Detail);
    }

    [Fact]
    public void GetDailyUsage_DefaultDates_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.GetDailyUsage(1, default, default);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("required", problem.Detail);
    }

    [Fact]
    public void GetMonthlyUsage_NonAdmin_Returns403()
    {
        var controller = CreateController(isAdmin: false);
        var result = controller.GetMonthlyUsage(1, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public void GetMonthlyUsage_InvalidTenantId_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.GetMonthlyUsage(-1, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("tenantId", problem.Detail);
    }

    [Fact]
    public void GetMonthlyUsage_FromAfterTo_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.GetMonthlyUsage(1, DateTime.UtcNow, DateTime.UtcNow.AddDays(-30));
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("from must be before", problem.Detail);
    }
}
