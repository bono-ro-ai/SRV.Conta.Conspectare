using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class DocumentResponse
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

    [JsonPropertyName("inputFormat")]
    public string? InputFormat { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("documentType")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("triageConfidence")]
    public decimal? TriageConfidence { get; set; }

    [JsonPropertyName("isAccountingRelevant")]
    public bool? IsAccountingRelevant { get; set; }

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("clientReference")]
    public string? ClientReference { get; set; }

    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("canonicalOutputJson")]
    public string? CanonicalOutputJson { get; set; }

    [JsonPropertyName("reviewFlags")]
    public List<ReviewFlagResponse>? ReviewFlags { get; set; }

    [JsonPropertyName("events")]
    public List<DocumentEventResponse>? Events { get; set; }

    [JsonPropertyName("extractionAttempts")]
    public List<ExtractionAttemptResponse>? ExtractionAttempts { get; set; }

    [JsonPropertyName("isTerminal")]
    public bool IsTerminal { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }
}
