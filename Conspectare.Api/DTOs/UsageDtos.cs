namespace Conspectare.Api.DTOs;

public record UsageDailyItem(
    DateTime UsageDate,
    int DocumentsIngested,
    int DocumentsProcessed,
    long LlmInputTokens,
    long LlmOutputTokens,
    int LlmRequests,
    long StorageBytes,
    int ApiCalls);

public record UsageDailyResponse(
    IReadOnlyList<UsageDailyItem> Items,
    long TenantId,
    DateTime From,
    DateTime To);

public record MonthlyUsageSummaryItem(
    int Year,
    int Month,
    int DocumentsIngested,
    int DocumentsProcessed,
    long LlmInputTokens,
    long LlmOutputTokens,
    int LlmRequests,
    long StorageBytes,
    int ApiCalls);

public record MonthlyUsageSummaryResponse(
    IReadOnlyList<MonthlyUsageSummaryItem> Items,
    long TenantId,
    DateTime From,
    DateTime To);
