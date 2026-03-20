namespace Conspectare.Api.DTOs;

public record QueueDepthItem(string Status, int Count);
public record QueueDepthsResponse(IReadOnlyList<QueueDepthItem> Items, int TotalDocuments);

public record ProcessingTimesResponse(double? P50Ms, double? P95Ms, int SampleCount, DateTime From, DateTime To);

public record ErrorRatesResponse(int TotalDocuments, int FailedDocuments, decimal ErrorRate, DateTime From, DateTime To);

public record LlmCostItem(string ModelId, long TotalInputTokens, long TotalOutputTokens, int AttemptCount);
public record LlmCostsResponse(IReadOnlyList<LlmCostItem> Items, long GrandTotalInputTokens, long GrandTotalOutputTokens, DateTime From, DateTime To);

public record VolumeItem(DateTime Date, int Count);
public record TenantVolumesResponse(IReadOnlyList<VolumeItem> Items, int TotalDocuments, DateTime From, DateTime To);
