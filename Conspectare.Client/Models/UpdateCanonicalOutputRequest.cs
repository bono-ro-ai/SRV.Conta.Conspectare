using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class UpdateCanonicalOutputRequest
{
    [JsonPropertyName("canonicalOutputJson")]
    public string CanonicalOutputJson { get; set; } = string.Empty;
}
