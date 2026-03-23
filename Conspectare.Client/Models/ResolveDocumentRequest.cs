using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class ResolveDocumentRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("canonicalOutputJson")]
    public string? CanonicalOutputJson { get; set; }
}
