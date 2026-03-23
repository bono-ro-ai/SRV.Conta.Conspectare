using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class ExtractionAttemptResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; }

    [JsonPropertyName("phase")]
    public string? Phase { get; set; }

    [JsonPropertyName("modelId")]
    public string? ModelId { get; set; }

    [JsonPropertyName("promptVersion")]
    public string? PromptVersion { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("inputTokens")]
    public int? InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public int? OutputTokens { get; set; }

    [JsonPropertyName("latencyMs")]
    public int? LatencyMs { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
