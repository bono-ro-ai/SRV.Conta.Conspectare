using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Conspectare.Tests;

public class AdminApiClientsControllerTests
{
    private AdminApiClientsController CreateController(bool isAdmin)
    {
        var tenantContext = new MockTenantContext { TenantId = 1, IsAdmin = isAdmin };
        return new AdminApiClientsController(tenantContext);
    }

    [Fact]
    public void Create_MissingName_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest(null, 60, 10, null);
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Name is required", problem.Detail);
    }

    [Fact]
    public void Create_EmptyName_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("   ", 60, 10, null);
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public void Create_NullBody_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var result = controller.Create(null);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public void Create_NotAdmin_Returns403()
    {
        var controller = CreateController(isAdmin: false);
        var request = new CreateApiClientRequest("Test Client", 60, 10, null);
        var result = controller.Create(request);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void List_NotAdmin_Returns403()
    {
        var controller = CreateController(isAdmin: false);
        var result = controller.List();
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void SoftDelete_NotAdmin_Returns403()
    {
        var controller = CreateController(isAdmin: false);
        var result = controller.SoftDelete(1);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
