using Conspectare.Api.Controllers;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class DocumentsControllerTests
{
    private static DocumentsController CreateController(int maxFileSizeMb, Mock<IDocumentService> docServiceMock = null)
    {
        docServiceMock ??= new Mock<IDocumentService>();
        var storageService = new Mock<IStorageService>();
        var workflow = new DocumentStatusWorkflow();
        var tenant = new MockTenantContext { TenantId = 1, MaxFileSizeMb = maxFileSizeMb, ApiKeyPrefix = "csp_test" };
        var logger = new Mock<ILogger<DocumentsController>>();
        return new DocumentsController(docServiceMock.Object, storageService.Object, workflow, tenant, logger.Object);
    }

    private static Mock<IDocumentService> CreateDocServiceReturningSuccess()
    {
        var mock = new Mock<IDocumentService>();
        mock.Setup(s => s.IngestAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.Created(new Document
            {
                Id = 1, DocumentRef = "CSP-000001", Status = "pending_triage", CreatedAt = DateTime.UtcNow
            }));
        return mock;
    }

    private static IFormFile CreateMockFile(long sizeBytes, string contentType = "application/pdf", string fileName = "test.pdf")
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(sizeBytes);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[0]));
        return fileMock.Object;
    }

    [Fact]
    public async Task Upload_FileExceedsMaxSize_Returns413()
    {
        var controller = CreateController(maxFileSizeMb: 10);
        var file = CreateMockFile(sizeBytes: 11 * 1024 * 1024);

        var result = await controller.Upload(file, "ref-1", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(413, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("10 MB", problem.Detail);
    }

    [Fact]
    public async Task Upload_FileWithinLimit_Proceeds()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(maxFileSizeMb: 10, docService);
        var file = CreateMockFile(sizeBytes: 5 * 1024 * 1024);

        var result = await controller.Upload(file, "ref-1", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    [Fact]
    public async Task Upload_MaxFileSizeZero_SkipsCheck()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(maxFileSizeMb: 0, docService);
        var file = CreateMockFile(sizeBytes: 100 * 1024 * 1024);

        var result = await controller.Upload(file, "ref-1", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }

    [Fact]
    public async Task Upload_FileExactlyAtLimit_Proceeds()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(maxFileSizeMb: 10, docService);
        var file = CreateMockFile(sizeBytes: 10 * 1024 * 1024);

        var result = await controller.Upload(file, "ref-1", null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
    }
}
