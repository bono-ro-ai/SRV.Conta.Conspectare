namespace Conspectare.Api.DTOs;

public record DocumentListResponse(
    IReadOnlyList<DocumentSummaryResponse> Items,
    int Total,
    int Page,
    int PageSize);
