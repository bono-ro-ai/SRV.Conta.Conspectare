using Conspectare.Api.Extensions;
using Conspectare.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Conspectare.Tests;

public class OperationResultExtensionsTests
{
    [Fact]
    public void ToActionResult_Success200_ReturnsOkObjectResult()
    {
        var result = OperationResult<string>.Success("hello");

        var actionResult = result.ToActionResult();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal("hello", okResult.Value);
    }

    [Fact]
    public void ToActionResult_Success201_ReturnsObjectResultWith201()
    {
        var result = OperationResult<string>.Created("created-item");

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.Equal("created-item", objectResult.Value);
    }

    [Fact]
    public void ToActionResult_Success202_ReturnsObjectResultWith202()
    {
        var result = OperationResult<string>.Accepted("accepted-item");

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status202Accepted, objectResult.StatusCode);
        Assert.Equal("accepted-item", objectResult.Value);
    }

    [Fact]
    public void ToActionResult_NotFound_ReturnsProblemDetails404()
    {
        var result = OperationResult<string>.NotFound("not found message");

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal("not found message", problem.Detail);
    }

    [Fact]
    public void ToActionResult_BadRequest_ReturnsProblemDetails400()
    {
        var result = OperationResult<string>.BadRequest("bad request message");

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal("bad request message", problem.Detail);
    }

    [Fact]
    public void ToActionResult_Conflict_ReturnsProblemDetails409()
    {
        var result = OperationResult<string>.Conflict("conflict message");

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Conflict", problem.Title);
        Assert.Equal("conflict message", problem.Detail);
    }

    [Fact]
    public void ToActionResult_BadRequestWithErrors_IncludesErrorsExtension()
    {
        var errors = new List<string> { "Error 1", "Error 2" };
        var result = OperationResult<string>.BadRequest(errors);

        var actionResult = result.ToActionResult();

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.True(problem.Extensions.ContainsKey("errors"));
    }
}
