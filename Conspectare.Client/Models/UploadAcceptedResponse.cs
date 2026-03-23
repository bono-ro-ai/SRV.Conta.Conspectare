using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class UploadAcceptedResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("documentRef")]
    public string? DocumentRef { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
