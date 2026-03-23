using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class DocumentEventResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("eventType")]
    public string? EventType { get; set; }

    [JsonPropertyName("fromStatus")]
    public string? FromStatus { get; set; }

    [JsonPropertyName("toStatus")]
    public string? ToStatus { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}
