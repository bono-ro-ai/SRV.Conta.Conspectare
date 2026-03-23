using System.Text.Json.Serialization;

namespace Conspectare.Client.Models;

public class DocumentListResponse
{
    [JsonPropertyName("items")]
    public List<DocumentSummaryResponse> Items { get; set; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }
}
