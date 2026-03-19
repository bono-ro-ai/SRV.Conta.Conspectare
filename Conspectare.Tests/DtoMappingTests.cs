using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class DtoMappingTests
{
    private readonly DocumentStatusWorkflow _workflow = new();

    private Document CreateTestDocument(string status = DocumentStatus.PendingTriage) => new()
    {
        Id = 42,
        TenantId = 1,
        ExternalRef = "ext-ref-001",
        FileName = "invoice.xml",
        ContentType = "text/xml",
        FileSizeBytes = 1024,
        InputFormat = InputFormat.XmlEfactura,
        Status = status,
        DocumentType = "invoice",
        TriageConfidence = 0.95m,
        IsAccountingRelevant = true,
        RetryCount = 0,
        MaxRetries = 3,
        ClientReference = "client-ref",
        Metadata = "{\"key\":\"value\"}",
        CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 1, 15, 10, 5, 0, DateTimeKind.Utc),
        ReviewFlags = new List<ReviewFlag>(),
        Artifacts = new List<DocumentArtifact>(),
        Events = new List<DocumentEvent>()
    };

    [Fact]
    public void DocumentResponse_FromEntity_MapsAllFields()
    {
        var document = CreateTestDocument(DocumentStatus.Completed);
        document.CanonicalOutput = new CanonicalOutput
        {
            Id = 1,
            DocumentId = 42,
            TenantId = 1,
            OutputJson = "{\"invoiceNumber\":\"F001\"}",
            SchemaVersion = "1.0"
        };

        var response = DocumentResponse.FromEntity(document, _workflow);

        Assert.Equal(42, response.Id);
        Assert.Equal("ext-ref-001", response.ExternalRef);
        Assert.Equal("invoice.xml", response.FileName);
        Assert.Equal("text/xml", response.ContentType);
        Assert.Equal(1024, response.FileSizeBytes);
        Assert.Equal(InputFormat.XmlEfactura, response.InputFormat);
        Assert.Equal("completed", response.Status);
        Assert.Equal("invoice", response.DocumentType);
        Assert.Equal(0.95m, response.TriageConfidence);
        Assert.True(response.IsAccountingRelevant);
        Assert.Equal(0, response.RetryCount);
        Assert.Equal(3, response.MaxRetries);
        Assert.Equal("client-ref", response.ClientReference);
        Assert.Equal("{\"key\":\"value\"}", response.Metadata);
        Assert.Equal("{\"invoiceNumber\":\"F001\"}", response.CanonicalOutputJson);
        Assert.Empty(response.ReviewFlags);
    }

    [Fact]
    public void DocumentResponse_FromEntity_MapsExternalStatusCorrectly()
    {
        var completed = CreateTestDocument(DocumentStatus.Completed);
        completed.CompletedAt = DateTime.UtcNow;

        var response = DocumentResponse.FromEntity(completed, _workflow);
        Assert.Equal("completed", response.Status);

        var failed = CreateTestDocument(DocumentStatus.Failed);
        var failedResponse = DocumentResponse.FromEntity(failed, _workflow);
        Assert.Equal("failed", failedResponse.Status);

        var review = CreateTestDocument(DocumentStatus.ReviewRequired);
        var reviewResponse = DocumentResponse.FromEntity(review, _workflow);
        Assert.Equal("review_required", reviewResponse.Status);
    }

    [Fact]
    public void DocumentResponse_FromEntity_MapsReviewFlags()
    {
        var document = CreateTestDocument();
        document.ReviewFlags = new List<ReviewFlag>
        {
            new()
            {
                Id = 1,
                DocumentId = 42,
                TenantId = 1,
                FlagType = "missing_field",
                Severity = "warning",
                Message = "Invoice number missing",
                IsResolved = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        var response = DocumentResponse.FromEntity(document, _workflow);

        Assert.Single(response.ReviewFlags);
        Assert.Equal("missing_field", response.ReviewFlags[0].FlagType);
        Assert.Equal("warning", response.ReviewFlags[0].Severity);
        Assert.Equal("Invoice number missing", response.ReviewFlags[0].Message);
        Assert.False(response.ReviewFlags[0].IsResolved);
    }

    [Fact]
    public void DocumentResponse_FromEntity_NullCanonicalOutput_ReturnsNull()
    {
        var document = CreateTestDocument();
        document.CanonicalOutput = null;

        var response = DocumentResponse.FromEntity(document, _workflow);

        Assert.Null(response.CanonicalOutputJson);
    }

    [Fact]
    public void DocumentSummaryResponse_FromEntity_MapsFields()
    {
        var document = CreateTestDocument();

        var response = DocumentSummaryResponse.FromEntity(document, _workflow);

        Assert.Equal(42, response.Id);
        Assert.Equal("ext-ref-001", response.ExternalRef);
        Assert.Equal("invoice.xml", response.FileName);
        Assert.Equal("processing", response.Status);
        Assert.Equal(0, response.RetryCount);
        Assert.Equal("client-ref", response.ClientReference);
    }

    [Fact]
    public void ReviewFlagResponse_FromEntity_MapsAllFields()
    {
        var flag = new ReviewFlag
        {
            Id = 10,
            DocumentId = 42,
            TenantId = 1,
            FlagType = "low_confidence",
            Severity = "error",
            Message = "Confidence below threshold",
            IsResolved = true,
            ResolvedAt = new DateTime(2026, 1, 16, 12, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        var response = ReviewFlagResponse.FromEntity(flag);

        Assert.Equal(10, response.Id);
        Assert.Equal("low_confidence", response.FlagType);
        Assert.Equal("error", response.Severity);
        Assert.Equal("Confidence below threshold", response.Message);
        Assert.True(response.IsResolved);
        Assert.Equal(new DateTime(2026, 1, 16, 12, 0, 0, DateTimeKind.Utc), response.ResolvedAt);
    }
}
