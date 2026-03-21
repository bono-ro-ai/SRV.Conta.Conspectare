using System.Text.Json;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class WebhookPayloadBuilderTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_CompletedDocument_IncludesResultSummary()
    {
        var canonicalOutput = new CanonicalOutput
        {
            OutputJson = "{\"invoice_number\":\"FAC-001\",\"total_amount\":1190.00}"
        };
        var doc = new Document
        {
            Id = 42,
            TenantId = 1,
            ExternalRef = "ext-ref-001",
            Status = DocumentStatus.Completed,
            CanonicalOutput = canonicalOutput
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("document.status_changed", root.GetProperty("event").GetString());
        Assert.Equal(42, root.GetProperty("document_id").GetInt64());
        Assert.Equal("ext-ref-001", root.GetProperty("external_ref").GetString());
        Assert.Equal("completed", root.GetProperty("status").GetString());
        Assert.Equal("FAC-001", root.GetProperty("result_summary").GetProperty("invoice_number").GetString());
        Assert.False(root.TryGetProperty("error_message", out _));
    }

    [Fact]
    public void Build_FailedDocument_IncludesErrorMessage()
    {
        var doc = new Document
        {
            Id = 43,
            TenantId = 1,
            Status = DocumentStatus.Failed,
            ErrorMessage = "LLM extraction timeout"
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("failed", root.GetProperty("status").GetString());
        Assert.Equal("LLM extraction timeout", root.GetProperty("error_message").GetString());
        Assert.False(root.TryGetProperty("result_summary", out _));
    }

    [Fact]
    public void Build_RejectedDocument_HasCorrectStatus()
    {
        var doc = new Document
        {
            Id = 44,
            TenantId = 1,
            Status = DocumentStatus.Rejected,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("rejected", root.GetProperty("status").GetString());
        Assert.Equal(44, root.GetProperty("document_id").GetInt64());
    }

    [Fact]
    public void Build_ReviewRequiredDocument_HasCorrectStatus()
    {
        var doc = new Document
        {
            Id = 45,
            TenantId = 1,
            ExternalRef = "review-doc",
            Status = DocumentStatus.ReviewRequired,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("review_required", root.GetProperty("status").GetString());
        Assert.Equal("review-doc", root.GetProperty("external_ref").GetString());
    }

    [Fact]
    public void Build_IncludesTimestamp()
    {
        var doc = new Document
        {
            Id = 46,
            TenantId = 1,
            Status = DocumentStatus.Completed,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        var timestamp = root.GetProperty("timestamp").GetString();
        Assert.Contains("2026-03-20", timestamp);
    }

    [Fact]
    public void Build_NullExternalRef_IncludesNullInPayload()
    {
        var doc = new Document
        {
            Id = 47,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            ExternalRef = null,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(JsonValueKind.Null, root.GetProperty("external_ref").ValueKind);
    }

    [Fact]
    public void Build_CompletedDocument_IncludesClientReference()
    {
        var doc = new Document
        {
            Id = 50,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            ClientReference = "my-client-ref-123",
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("my-client-ref-123", root.GetProperty("client_reference").GetString());
    }

    [Fact]
    public void Build_CompletedDocument_IncludesCanonicalOutputJson()
    {
        var rawJson = "{\"invoice_number\":\"FAC-001\",\"total_amount\":1190.00}";
        var canonicalOutput = new CanonicalOutput { OutputJson = rawJson };
        var doc = new Document
        {
            Id = 51,
            TenantId = 1,
            Status = DocumentStatus.Completed,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow, canonicalOutput);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(rawJson, root.GetProperty("canonical_output_json").GetString());
    }

    [Fact]
    public void Build_CompletedDocument_IncludesDocumentType()
    {
        var doc = new Document
        {
            Id = 52,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            DocumentType = "invoice",
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal("invoice", root.GetProperty("document_type").GetString());
    }

    [Fact]
    public void Build_CompletedDocument_IncludesConfidence()
    {
        var doc = new Document
        {
            Id = 53,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            TriageConfidence = 0.95m,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Equal(0.95m, root.GetProperty("confidence").GetDecimal());
    }

    [Fact]
    public void Build_CompletedDocument_IncludesCompletedAt()
    {
        var completedAt = new DateTime(2026, 3, 20, 14, 30, 0, DateTimeKind.Utc);
        var doc = new Document
        {
            Id = 54,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            CompletedAt = completedAt,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.Contains("2026-03-20T14:30:00", root.GetProperty("completed_at").GetString());
    }

    [Fact]
    public void Build_DocumentWithReviewFlags_IncludesFlagArray()
    {
        var doc = new Document
        {
            Id = 55,
            TenantId = 1,
            Status = DocumentStatus.ReviewRequired,
        };
        var flags = new List<ReviewFlag>
        {
            new() { FlagType = "confidence_low", Severity = "warning", Message = "Low confidence", IsResolved = false },
            new() { FlagType = "vat_mismatch", Severity = "error", Message = "VAT mismatch", IsResolved = true },
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow, reviewFlags: flags);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        var flagsArr = root.GetProperty("review_flags");
        Assert.Equal(JsonValueKind.Array, flagsArr.ValueKind);
        Assert.Equal(2, flagsArr.GetArrayLength());
        Assert.Equal("confidence_low", flagsArr[0].GetProperty("flag_type").GetString());
        Assert.Equal("warning", flagsArr[0].GetProperty("severity").GetString());
        Assert.Equal("Low confidence", flagsArr[0].GetProperty("message").GetString());
        Assert.False(flagsArr[0].GetProperty("is_resolved").GetBoolean());
        Assert.Equal("vat_mismatch", flagsArr[1].GetProperty("flag_type").GetString());
        Assert.True(flagsArr[1].GetProperty("is_resolved").GetBoolean());
    }

    [Fact]
    public void Build_EmptyReviewFlags_SerializesAsEmptyArray()
    {
        var doc = new Document
        {
            Id = 56,
            TenantId = 1,
            Status = DocumentStatus.Completed,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow, reviewFlags: new List<ReviewFlag>());
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        var flagsArr = root.GetProperty("review_flags");
        Assert.Equal(JsonValueKind.Array, flagsArr.ValueKind);
        Assert.Equal(0, flagsArr.GetArrayLength());
    }

    [Fact]
    public void Build_NullCanonicalOutput_CanonicalOutputJsonIsNull()
    {
        var doc = new Document
        {
            Id = 57,
            TenantId = 1,
            Status = DocumentStatus.Completed,
            CanonicalOutput = null,
        };
        var json = WebhookPayloadBuilder.Build(doc, FixedUtcNow, canonicalOutput: null);
        var parsed = JsonDocument.Parse(json);
        var root = parsed.RootElement;
        Assert.False(root.TryGetProperty("canonical_output_json", out _));
        Assert.False(root.TryGetProperty("result_summary", out _));
    }
}
