using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Conspectare.Services.Commands;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Queries;
using Conspectare.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Conspectare.Tests;

public class AdminApiClientsControllerTests
{
    private AdminApiClientsController CreateController(bool isAdmin)
    {
        var tenantContext = new MockTenantContext { TenantId = 1, IsAdmin = isAdmin, ApiKeyPrefix = "csp_admi" };
        var logger = NullLogger<AdminApiClientsController>.Instance;
        return new AdminApiClientsController(tenantContext, logger);
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
    public void Create_NameTooLong_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var longName = new string('A', 201);
        var request = new CreateApiClientRequest(longName, 60, 10, null);
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("200 characters", problem.Detail);
    }

    [Fact]
    public void Create_ZeroRateLimit_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("Test", 0, 10, null);
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("RateLimitPerMin", problem.Detail);
    }

    [Fact]
    public void Create_NegativeMaxFileSize_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("Test", 60, -1, null);
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("MaxFileSizeMb", problem.Detail);
    }

    [Fact]
    public void Create_InvalidWebhookUrl_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("Test", 60, 10, "not-a-url");
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("WebhookUrl", problem.Detail);
    }

    [Fact]
    public void Create_FtpWebhookUrl_Returns400()
    {
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("Test", 60, 10, "ftp://example.com/hook");
        var result = controller.Create(request);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("WebhookUrl", problem.Detail);
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

    [Fact]
    public void Create_ValidRequest_Returns201WithApiKey()
    {
        using var helper = new TestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(helper);
        var controller = CreateController(isAdmin: true);
        var request = new CreateApiClientRequest("Integration Test Client", 120, 25, "https://example.com/hook");
        var result = controller.Create(request);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var response = Assert.IsType<CreateApiClientResponse>(objectResult.Value);
        Assert.Equal("Integration Test Client", response.Name);
        Assert.StartsWith("csp_", response.ApiKey);
        Assert.Equal(68, response.ApiKey.Length);
        Assert.Equal(response.ApiKey[..8], response.ApiKeyPrefix);
        Assert.True(response.Id > 0);
    }

    [Fact]
    public void List_ReturnsClients()
    {
        using var helper = new TestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(helper);
        var controller = CreateController(isAdmin: true);
        controller.Create(new CreateApiClientRequest("Client A", 60, 10, null));
        controller.Create(new CreateApiClientRequest("Client B", 120, 20, "https://example.com/hook"));
        var result = controller.List();
        var okResult = Assert.IsType<OkObjectResult>(result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<ApiClientListItem>>(okResult.Value);
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Client A");
        Assert.Contains(items, i => i.Name == "Client B");
    }

    [Fact]
    public void SoftDelete_ExistingClient_Returns204()
    {
        using var helper = new TestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(helper);
        var controller = CreateController(isAdmin: true);
        var createResult = (ObjectResult)controller.Create(new CreateApiClientRequest("ToDelete", 60, 10, null));
        var created = (CreateApiClientResponse)createResult.Value;
        var deleteResult = controller.SoftDelete(created.Id);
        Assert.IsType<NoContentResult>(deleteResult);
    }

    [Fact]
    public void SoftDelete_NonExistentId_Returns404()
    {
        using var helper = new TestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(helper);
        var controller = CreateController(isAdmin: true);
        var result = controller.SoftDelete(99999);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }
}
