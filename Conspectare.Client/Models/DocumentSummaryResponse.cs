using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class DocumentSummaryResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("documentRef")]
    public string? DocumentRef { get; set; }

    [JsonPropertyName("fiscalCode")]
    public string? FiscalCode { get; set; }

    [JsonPropertyName("externalRef")]
    public string? ExternalRef { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("isTerminal")]
    public bool IsTerminal { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
