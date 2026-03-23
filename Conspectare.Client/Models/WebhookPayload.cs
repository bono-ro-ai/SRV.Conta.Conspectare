using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class WebhookPayload
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("document_id")]
    public long DocumentId { get; set; }

    [JsonPropertyName("document_ref")]
    public string? DocumentRef { get; set; }

    [JsonPropertyName("fiscal_code")]
    public string? FiscalCode { get; set; }

    [JsonPropertyName("external_ref")]
    public string? ExternalRef { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("client_reference")]
    public string? ClientReference { get; set; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("result_summary")]
    public object? ResultSummary { get; set; }

    [JsonPropertyName("canonical_output_json")]
    public string? CanonicalOutputJson { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("review_flags")]
    public List<WebhookReviewFlag>? ReviewFlags { get; set; }
}

public class WebhookReviewFlag
{
    [JsonPropertyName("flag_type")]
    public string? FlagType { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("is_resolved")]
    public bool IsResolved { get; set; }
}
