using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class BatchUploadResponse
{
    [JsonPropertyName("results")]
    public List<BatchUploadItemResult> Results { get; set; } = [];

    [JsonPropertyName("totalFiles")]
    public int TotalFiles { get; set; }

    [JsonPropertyName("succeeded")]
    public int Succeeded { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }
}

public class BatchUploadItemResult
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("id")]
    public long? Id { get; set; }

    [JsonPropertyName("documentRef")]
    public string? DocumentRef { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }
}
