namespace Conspectare.Services.Models;

public record TriageResult(
    string DocumentType,
    decimal Confidence,
    bool IsAccountingRelevant,
    string ModelId,
    string PromptVersion,
    int? InputTokens,
    int? OutputTokens,
    int? LatencyMs);
