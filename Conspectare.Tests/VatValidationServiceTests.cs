using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class VatValidationServiceTests
{
    private static Document CreateTestDocument(
        string supplierCui = null, string customerCui = null)
    {
        var doc = new Document
        {
            Id = 1,
            TenantId = 100,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            InputFormat = "pdf",
            Status = "completed",
            RawFileS3Key = "tenant-100/raw/test.pdf",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        doc.CanonicalOutput = new CanonicalOutput
        {
            Id = 10,
            DocumentId = doc.Id,
            TenantId = doc.TenantId,
            SchemaVersion = "1.0.0",
            OutputJson = "{}",
            SupplierCui = supplierCui,
            CustomerCui = customerCui,
            CreatedAt = DateTime.UtcNow,
            Document = doc
        };

        return doc;
    }

    private static TestableVatValidationService CreateService(Mock<IAnafVatValidationClient> mockClient)
    {
        return new TestableVatValidationService(
            mockClient.Object,
            NullLogger<VatValidationService>.Instance);
    }

    private class TestableVatValidationService(
        IAnafVatValidationClient anafClient,
        Microsoft.Extensions.Logging.ILogger<VatValidationService> logger)
        : VatValidationService(anafClient, logger)
    {
        public IList<(string role, AnafValidationResult result)> SavedResults { get; private set; }

        protected override void SaveValidationResults(
            Document document, IList<(string role, AnafValidationResult result)> validationResults)
        {
            SavedResults = validationResults;
        }
    }

    [Fact]
    public async Task ValidateDocument_NoCuisInCanonicalOutput_SkipsValidation()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        var service = CreateService(mockClient);
        var doc = CreateTestDocument(null, null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateDocument_NoCanonicalOutput_SkipsValidation()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        var service = CreateService(mockClient);
        var doc = new Document
        {
            Id = 1,
            TenantId = 100,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            InputFormat = "pdf",
            Status = "completed",
            RawFileS3Key = "tenant-100/raw/test.pdf",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CanonicalOutput = null
        };

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateDocument_EmptyWhitespaceCuis_SkipsValidation()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        var service = CreateService(mockClient);
        var doc = CreateTestDocument("  ", "");

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ValidateDocument_SupplierCuiOnly_ValidatesOnlySupplier()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        mockClient
            .Setup(c => c.ValidateCuiAsync("RO12345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnafValidationResult(
                IsValid: true,
                Cui: "RO12345678",
                CompanyName: "Test Supplier",
                IsInactive: false,
                ValidationError: null));

        var service = CreateService(mockClient);
        var doc = CreateTestDocument("RO12345678", null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync("RO12345678", It.IsAny<CancellationToken>()), Times.Once);
        mockClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidateDocument_BothCuis_CallsAnafForBoth()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        mockClient
            .Setup(c => c.ValidateCuiAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cui, CancellationToken _) => new AnafValidationResult(
                IsValid: true,
                Cui: cui,
                CompanyName: "Test Company",
                IsInactive: false,
                ValidationError: null));

        var service = CreateService(mockClient);
        var doc = CreateTestDocument("RO12345678", "RO87654321");

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync("RO12345678", It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(
            c => c.ValidateCuiAsync("RO87654321", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateDocument_AnafThrowsForSupplier_StillValidatesCustomer()
    {
        var mockClient = new Mock<IAnafVatValidationClient>();
        mockClient
            .Setup(c => c.ValidateCuiAsync("RO12345678", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        mockClient
            .Setup(c => c.ValidateCuiAsync("RO87654321", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnafValidationResult(
                IsValid: true,
                Cui: "RO87654321",
                CompanyName: "Customer SRL",
                IsInactive: false,
                ValidationError: null));

        var service = CreateService(mockClient);
        var doc = CreateTestDocument("RO12345678", "RO87654321");

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        mockClient.Verify(
            c => c.ValidateCuiAsync("RO12345678", It.IsAny<CancellationToken>()), Times.Once);
        mockClient.Verify(
            c => c.ValidateCuiAsync("RO87654321", It.IsAny<CancellationToken>()), Times.Once);
    }
}
