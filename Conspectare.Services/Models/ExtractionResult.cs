namespace Conspectare.Services.Models;

public record ExtractionResult(
    string OutputJson,
    string SchemaVersion,
    string ModelId,
    string PromptVersion,
    int? InputTokens,
    int? OutputTokens,
    int? LatencyMs,
    List<ReviewFlagInfo> ReviewFlags);
