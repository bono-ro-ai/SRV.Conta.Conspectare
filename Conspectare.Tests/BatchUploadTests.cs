using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class BatchUploadTests
{
    private static DocumentsController CreateController(int maxFileSizeMb = 50, Mock<IDocumentService> docServiceMock = null)
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
        var callCount = 0;
        var mock = new Mock<IDocumentService>();
        mock.Setup(s => s.IngestAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var id = Interlocked.Increment(ref callCount);
                return OperationResult<Document>.Created(new Document
                {
                    Id = id, DocumentRef = $"CSP-{id:D6}", Status = "pending_triage", CreatedAt = DateTime.UtcNow
                });
            });
        return mock;
    }

    private static IFormFile CreateMockFile(long sizeBytes = 1024, string contentType = "application/pdf", string fileName = "test.pdf")
    {
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(sizeBytes);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[0]));
        return fileMock.Object;
    }

    private static IFormFileCollection CreateFileCollection(params IFormFile[] files)
    {
        var collection = new FormFileCollection();
        collection.AddRange(files);
        return collection;
    }

    [Fact]
    public async Task BatchUpload_NoFiles_Returns400()
    {
        var controller = CreateController();
        var emptyCollection = new FormFileCollection();

        var result = await controller.BatchUpload(emptyCollection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("At least one file is required", problem.Detail);
    }

    [Fact]
    public async Task BatchUpload_NullFiles_Returns400()
    {
        var controller = CreateController();

        var result = await controller.BatchUpload(null, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("At least one file is required", problem.Detail);
    }

    [Fact]
    public async Task BatchUpload_TooManyFiles_Returns400()
    {
        var controller = CreateController();
        var files = Enumerable.Range(0, 21).Select(_ => CreateMockFile()).ToArray();
        var collection = CreateFileCollection(files);

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Contains("Maximum 20 files", problem.Detail);
    }

    [Fact]
    public async Task BatchUpload_AllValid_Returns202()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(docServiceMock: docService);
        var files = new[] { CreateMockFile(fileName: "a.pdf"), CreateMockFile(fileName: "b.pdf") };
        var collection = CreateFileCollection(files);

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(2, response.TotalFiles);
        Assert.Equal(2, response.Succeeded);
        Assert.Equal(0, response.Failed);
        Assert.All(response.Results, r => Assert.Null(r.Error));
    }

    [Fact]
    public async Task BatchUpload_MixedValidAndInvalid_Returns207()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(docServiceMock: docService);
        var validFile = CreateMockFile(fileName: "good.pdf");
        var invalidFile = CreateMockFile(fileName: "bad.exe", contentType: "application/x-msdownload");
        var collection = CreateFileCollection(validFile, invalidFile);

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status207MultiStatus, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(2, response.TotalFiles);
        Assert.Equal(1, response.Succeeded);
        Assert.Equal(1, response.Failed);
        Assert.Null(response.Results[0].Error);
        Assert.NotNull(response.Results[1].Error);
        Assert.Contains("not supported", response.Results[1].Error);
    }

    [Fact]
    public async Task BatchUpload_ServiceFailure_Returns207()
    {
        var docService = new Mock<IDocumentService>();
        docService.Setup(s => s.IngestAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<Document>.ServerError("Storage unavailable"));
        var controller = CreateController(docServiceMock: docService);
        var collection = CreateFileCollection(CreateMockFile());

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status207MultiStatus, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(1, response.Failed);
        Assert.Contains("Storage unavailable", response.Results[0].Error);
    }

    [Fact]
    public async Task BatchUpload_SingleFile_Returns202()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(docServiceMock: docService);
        var collection = CreateFileCollection(CreateMockFile(fileName: "single.pdf"));

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(1, response.TotalFiles);
        Assert.Equal(1, response.Succeeded);
        Assert.Equal(0, response.Failed);
    }

    [Fact]
    public async Task BatchUpload_WithRequestId_SetsExternalRefPerFile()
    {
        var capturedRefs = new List<string>();
        var docService = new Mock<IDocumentService>();
        docService.Setup(s => s.IngestAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, string, string, string, string, string, CancellationToken>((_, _, _, extRef, _, _, _, _) => capturedRefs.Add(extRef))
            .ReturnsAsync(OperationResult<Document>.Created(new Document
            {
                Id = 1, DocumentRef = "CSP-000001", Status = "pending_triage", CreatedAt = DateTime.UtcNow
            }));
        var controller = CreateController(docServiceMock: docService);
        var collection = CreateFileCollection(CreateMockFile(fileName: "a.pdf"), CreateMockFile(fileName: "b.pdf"));

        await controller.BatchUpload(collection, "req-123", null, null, null, CancellationToken.None);

        Assert.Equal(2, capturedRefs.Count);
        Assert.Equal("req-123:0", capturedRefs[0]);
        Assert.Equal("req-123:1", capturedRefs[1]);
    }

    [Fact]
    public async Task BatchUpload_FileTooLarge_ReportsPerFileError()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(maxFileSizeMb: 1, docServiceMock: docService);
        var smallFile = CreateMockFile(sizeBytes: 512 * 1024, fileName: "small.pdf");
        var largeFile = CreateMockFile(sizeBytes: 2 * 1024 * 1024, fileName: "large.pdf");
        var collection = CreateFileCollection(smallFile, largeFile);

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status207MultiStatus, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(1, response.Succeeded);
        Assert.Equal(1, response.Failed);
        Assert.Null(response.Results[0].Error);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, response.Results[1].StatusCode);
    }

    [Fact]
    public async Task BatchUpload_Exactly20Files_Succeeds()
    {
        var docService = CreateDocServiceReturningSuccess();
        var controller = CreateController(docServiceMock: docService);
        var files = Enumerable.Range(0, 20).Select(i => CreateMockFile(fileName: $"file{i}.pdf")).ToArray();
        var collection = CreateFileCollection(files);

        var result = await controller.BatchUpload(collection, null, null, null, null, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, objectResult.StatusCode);
        var response = Assert.IsType<BatchUploadResponse>(objectResult.Value);
        Assert.Equal(20, response.TotalFiles);
        Assert.Equal(20, response.Succeeded);
    }
}
