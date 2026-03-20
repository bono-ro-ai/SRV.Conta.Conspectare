using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class ReviewQueueControllerTests
{
    private readonly Mock<IReviewService> _reviewServiceMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly MockTenantContext _tenantContext = new() { TenantId = 1 };
    private readonly ReviewQueueController _controller;

    public ReviewQueueControllerTests()
    {
        _controller = new ReviewQueueController(
            _reviewServiceMock.Object,
            _storageServiceMock.Object,
            _tenantContext,
            NullLogger<ReviewQueueController>.Instance);
    }

    [Fact]
    public async Task Approve_DelegatesToService()
    {
        var doc = CreateTestDocument();
        _reviewServiceMock
            .Setup(s => s.ApproveAsync(1, 42, "ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.Success(doc));

        var result = await _controller.Approve(42, new ApproveDocumentRequest { Notes = "ok" }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReviewQueueItemResponse>(okResult.Value);
        Assert.Equal(42, response.Id);
        _reviewServiceMock.Verify(s => s.ApproveAsync(1, 42, "ok", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_EmptyReason_ReturnsBadRequest()
    {
        var result = await _controller.Reject(42, new RejectDocumentRequest { Reason = "" }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Reason is required", problem.Detail);
    }

    [Fact]
    public async Task Reject_NullRequest_ReturnsBadRequest()
    {
        var result = await _controller.Reject(42, null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public async Task Reject_ValidReason_DelegatesToService()
    {
        var doc = CreateTestDocument();
        _reviewServiceMock
            .Setup(s => s.RejectAsync(1, 42, "bad doc", It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.Success(doc));

        var result = await _controller.Reject(42, new RejectDocumentRequest { Reason = "bad doc" }, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ReviewQueueItemResponse>(okResult.Value);
        _reviewServiceMock.Verify(s => s.RejectAsync(1, 42, "bad doc", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void List_InvalidPage_ReturnsBadRequest()
    {
        var result = _controller.List(page: 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("Page must be >= 1", problem.Detail);
    }

    [Fact]
    public void List_InvalidPageSize_ReturnsBadRequest()
    {
        var result = _controller.List(pageSize: 201);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("PageSize must be between 1 and 200", problem.Detail);
    }

    [Fact]
    public async Task Approve_ServiceReturnsNotFound_ReturnsNotFound()
    {
        _reviewServiceMock
            .Setup(s => s.ApproveAsync(1, 999, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.NotFound("Document with id 999 not found."));

        var result = await _controller.Approve(999, new ApproveDocumentRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Approve_ServiceReturnsConflict_ReturnsConflict()
    {
        _reviewServiceMock
            .Setup(s => s.ApproveAsync(1, 42, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.Conflict("Cannot approve document in status 'completed'."));

        var result = await _controller.Approve(42, new ApproveDocumentRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    private static Document CreateTestDocument()
    {
        return new Document
        {
            Id = 42,
            TenantId = 1,
            ExternalRef = "ext-001",
            FileName = "invoice.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            InputFormat = InputFormat.Pdf,
            Status = DocumentStatus.Completed,
            DocumentType = "invoice",
            TriageConfidence = 0.95m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ReviewFlags = new List<ReviewFlag>()
        };
    }
}
