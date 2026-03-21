using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class DtoMappingTests
{
    private readonly DocumentStatusWorkflow _workflow = new();

    [Fact]
    public void DocumentEventResponse_FromEntity_MapsAllFields()
    {
        var entity = new DocumentEvent
        {
            Id = 1,
            EventType = "status_change",
            FromStatus = DocumentStatus.PendingTriage,
            ToStatus = DocumentStatus.Triaging,
            Details = "Triage started",
            CreatedAt = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc)
        };

        var result = DocumentEventResponse.FromEntity(entity);

        Assert.Equal(1, result.Id);
        Assert.Equal("status_change", result.EventType);
        Assert.Equal(DocumentStatus.PendingTriage, result.FromStatus);
        Assert.Equal(DocumentStatus.Triaging, result.ToStatus);
        Assert.Equal("Triage started", result.Details);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
    }

    [Fact]
    public void DocumentEventResponse_FromEntity_NullDetails_MapsAsNull()
    {
        var entity = new DocumentEvent
        {
            Id = 2,
            EventType = "ingestion",
            FromStatus = "",
            ToStatus = DocumentStatus.Ingested,
            Details = null,
            CreatedAt = DateTime.UtcNow
        };

        var result = DocumentEventResponse.FromEntity(entity);

        Assert.Null(result.Details);
    }

    [Fact]
    public void ExtractionAttemptResponse_FromEntity_MapsAllFields()
    {
        var entity = new ExtractionAttempt
        {
            Id = 10,
            AttemptNumber = 1,
            Phase = "extraction",
            ModelId = "gpt-4",
            PromptVersion = "v2.1",
            Status = "completed",
            InputTokens = 500,
            OutputTokens = 200,
            LatencyMs = 1200,
            Confidence = 0.95m,
            ErrorMessage = null,
            CreatedAt = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 3, 20, 10, 0, 2, DateTimeKind.Utc)
        };

        var result = ExtractionAttemptResponse.FromEntity(entity);

        Assert.Equal(10, result.Id);
        Assert.Equal(1, result.AttemptNumber);
        Assert.Equal("extraction", result.Phase);
        Assert.Equal("gpt-4", result.ModelId);
        Assert.Equal("v2.1", result.PromptVersion);
        Assert.Equal("completed", result.Status);
        Assert.Equal(500, result.InputTokens);
        Assert.Equal(200, result.OutputTokens);
        Assert.Equal(1200, result.LatencyMs);
        Assert.Equal(0.95m, result.Confidence);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);
        Assert.Equal(entity.CompletedAt, result.CompletedAt);
    }

    [Fact]
    public void ExtractionAttemptResponse_FromEntity_NullOptionalFields_MapsAsNull()
    {
        var entity = new ExtractionAttempt
        {
            Id = 11,
            AttemptNumber = 1,
            Phase = "extraction",
            ModelId = "gpt-4",
            PromptVersion = "v1.0",
            Status = "failed",
            InputTokens = null,
            OutputTokens = null,
            LatencyMs = null,
            Confidence = null,
            ErrorMessage = "Timeout",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = null
        };

        var result = ExtractionAttemptResponse.FromEntity(entity);

        Assert.Null(result.InputTokens);
        Assert.Null(result.OutputTokens);
        Assert.Null(result.LatencyMs);
        Assert.Null(result.Confidence);
        Assert.Null(result.CompletedAt);
        Assert.Equal("Timeout", result.ErrorMessage);
    }

    [Fact]
    public void DocumentResponse_FromEntity_WithEventsAndAttempts_MapsCollections()
    {
        var document = CreateTestDocument();
        document.Events = new List<DocumentEvent>
        {
            new() { Id = 1, EventType = "ingestion", FromStatus = "", ToStatus = DocumentStatus.Ingested, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = 2, EventType = "status_change", FromStatus = DocumentStatus.Ingested, ToStatus = DocumentStatus.PendingTriage, CreatedAt = DateTime.UtcNow.AddMinutes(-1) }
        };
        document.ExtractionAttempts = new List<ExtractionAttempt>
        {
            new() { Id = 10, AttemptNumber = 1, Phase = "extraction", ModelId = "gpt-4", PromptVersion = "v1", Status = "completed", CreatedAt = DateTime.UtcNow }
        };
        document.ReviewFlags = new List<ReviewFlag>
        {
            new() { Id = 20, FlagType = "confidence_low", Severity = "warning", Message = "Low confidence", IsResolved = false, CreatedAt = DateTime.UtcNow }
        };

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Equal(2, result.Events.Count);
        Assert.Single(result.ExtractionAttempts);
        Assert.Single(result.ReviewFlags);
        Assert.Equal(1, result.Events[0].Id);
        Assert.Equal(10, result.ExtractionAttempts[0].Id);
    }

    [Fact]
    public void DocumentResponse_FromEntity_EmptyCollections_ReturnsEmptyLists()
    {
        var document = CreateTestDocument();
        document.Events = new List<DocumentEvent>();
        document.ExtractionAttempts = new List<ExtractionAttempt>();
        document.ReviewFlags = new List<ReviewFlag>();

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Empty(result.Events);
        Assert.Empty(result.ExtractionAttempts);
        Assert.Empty(result.ReviewFlags);
    }

    [Fact]
    public void DocumentResponse_FromEntity_NullCollections_ReturnsEmptyLists()
    {
        var document = CreateTestDocument();
        document.Events = null;
        document.ExtractionAttempts = null;
        document.ReviewFlags = null;

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Empty(result.Events);
        Assert.Empty(result.ExtractionAttempts);
        Assert.Empty(result.ReviewFlags);
    }

    [Fact]
    public void DocumentResponse_FromEntity_CompletedStatus_IncludesCanonicalOutput()
    {
        var document = CreateTestDocument();
        document.Status = DocumentStatus.Completed;
        document.CanonicalOutput = new CanonicalOutput
        {
            Id = 1,
            OutputJson = "{\"invoiceNumber\":\"INV-001\"}",
            CreatedAt = DateTime.UtcNow
        };

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Equal("{\"invoiceNumber\":\"INV-001\"}", result.CanonicalOutputJson);
    }

    [Fact]
    public void DocumentResponse_FromEntity_AnyStatus_IncludesCanonicalOutput()
    {
        var document = CreateTestDocument();
        document.Status = DocumentStatus.PendingTriage;
        document.CanonicalOutput = new CanonicalOutput
        {
            Id = 1,
            OutputJson = "{\"invoiceNumber\":\"INV-001\"}",
            CreatedAt = DateTime.UtcNow
        };

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Equal("{\"invoiceNumber\":\"INV-001\"}", result.CanonicalOutputJson);
    }

    [Fact]
    public void DocumentResponse_FromEntity_EventsOrderedByCreatedAt()
    {
        var document = CreateTestDocument();
        var earlier = DateTime.UtcNow.AddMinutes(-10);
        var later = DateTime.UtcNow;
        document.Events = new List<DocumentEvent>
        {
            new() { Id = 2, EventType = "status_change", FromStatus = "", ToStatus = "", CreatedAt = later },
            new() { Id = 1, EventType = "ingestion", FromStatus = "", ToStatus = "", CreatedAt = earlier }
        };

        var result = DocumentResponse.FromEntity(document, _workflow);

        Assert.Equal(1, result.Events[0].Id);
        Assert.Equal(2, result.Events[1].Id);
    }

    [Fact]
    public void DocumentResponse_FromEntity_MapsTerminalState()
    {
        var completed = CreateTestDocument();
        completed.Status = DocumentStatus.Completed;

        var processing = CreateTestDocument();
        processing.Status = DocumentStatus.PendingTriage;

        Assert.True(DocumentResponse.FromEntity(completed, _workflow).IsTerminal);
        Assert.False(DocumentResponse.FromEntity(processing, _workflow).IsTerminal);
    }

    private static Document CreateTestDocument()
    {
        return new Document
        {
            Id = 1,
            TenantId = 1,
            ExternalRef = "ext-001",
            FileName = "test.xml",
            ContentType = "text/xml",
            FileSizeBytes = 1024,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.PendingTriage,
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Events = new List<DocumentEvent>(),
            ExtractionAttempts = new List<ExtractionAttempt>(),
            ReviewFlags = new List<ReviewFlag>()
        };
    }
}
