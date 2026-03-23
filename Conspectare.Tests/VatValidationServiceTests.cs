using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Microsoft.Extensions.Logging.Abstractions;
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

    private static TestableVatValidationService CreateService()
    {
        return new TestableVatValidationService(
            NullLogger<VatValidationService>.Instance);
    }

    private class TestableVatValidationService(
        Microsoft.Extensions.Logging.ILogger<VatValidationService> logger)
        : VatValidationService(logger)
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
        var service = CreateService();
        var doc = CreateTestDocument(null, null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.Null(service.SavedResults);
    }

    [Fact]
    public async Task ValidateDocument_NoCanonicalOutput_SkipsValidation()
    {
        var service = CreateService();
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

        Assert.Null(service.SavedResults);
    }

    [Fact]
    public async Task ValidateDocument_EmptyWhitespaceCuis_SkipsValidation()
    {
        var service = CreateService();
        var doc = CreateTestDocument("  ", "");

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.Null(service.SavedResults);
    }

    [Fact]
    public async Task ValidateDocument_ValidSupplierCui_SavesValidResult()
    {
        var service = CreateService();
        var doc = CreateTestDocument("RO16393852", null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.NotNull(service.SavedResults);
        Assert.Single(service.SavedResults);
        Assert.Equal("supplier", service.SavedResults[0].role);
        Assert.True(service.SavedResults[0].result.IsValid);
    }

    [Fact]
    public async Task ValidateDocument_InvalidSupplierCui_SavesInvalidResult()
    {
        var service = CreateService();
        var doc = CreateTestDocument("RO12345679", null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.NotNull(service.SavedResults);
        Assert.Single(service.SavedResults);
        Assert.Equal("supplier", service.SavedResults[0].role);
        Assert.False(service.SavedResults[0].result.IsValid);
        Assert.Contains("invalid check digit", service.SavedResults[0].result.ValidationError);
    }

    [Fact]
    public async Task ValidateDocument_BothCuis_ValidatesBoth()
    {
        var service = CreateService();
        var doc = CreateTestDocument("RO16393852", "RO16393852");

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.NotNull(service.SavedResults);
        Assert.Equal(2, service.SavedResults.Count);
        Assert.Equal("supplier", service.SavedResults[0].role);
        Assert.Equal("customer", service.SavedResults[1].role);
        Assert.True(service.SavedResults[0].result.IsValid);
        Assert.True(service.SavedResults[1].result.IsValid);
    }

    [Fact]
    public async Task ValidateDocument_NonNumericCui_SavesInvalidResult()
    {
        var service = CreateService();
        var doc = CreateTestDocument("ABCDEF", null);

        await service.ValidateDocumentAsync(doc, CancellationToken.None);

        Assert.NotNull(service.SavedResults);
        Assert.Single(service.SavedResults);
        Assert.False(service.SavedResults[0].result.IsValid);
        Assert.Contains("not a valid numeric", service.SavedResults[0].result.ValidationError);
    }
}
