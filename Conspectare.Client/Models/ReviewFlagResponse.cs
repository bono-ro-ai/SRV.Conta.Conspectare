using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class ReviewFlagResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("flagType")]
    public string? FlagType { get; set; }

    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("isResolved")]
    public bool IsResolved { get; set; }

    [JsonPropertyName("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
