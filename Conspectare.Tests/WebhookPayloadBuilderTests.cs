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
        var doc = new Document
        {
            Id = 42,
            TenantId = 1,
            ExternalRef = "ext-ref-001",
            Status = DocumentStatus.Completed,
            CanonicalOutput = new CanonicalOutput
            {
                OutputJson = "{\"invoice_number\":\"FAC-001\",\"total_amount\":1190.00}"
            }
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
}
