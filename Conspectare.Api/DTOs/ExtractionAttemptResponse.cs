using Conspectare.Domain.Entities;

namespace Conspectare.Api.DTOs;

public record ExtractionAttemptResponse(
    long Id,
    int AttemptNumber,
    string Phase,
    string ModelId,
    string PromptVersion,
    string Status,
    int? InputTokens,
    int? OutputTokens,
    int? LatencyMs,
    decimal? Confidence,
    string ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt)
{
    public static ExtractionAttemptResponse FromEntity(ExtractionAttempt a) =>
        new(
            a.Id,
            a.AttemptNumber,
            a.Phase,
            a.ModelId,
            a.PromptVersion,
            a.Status,
            a.InputTokens,
            a.OutputTokens,
            a.LatencyMs,
            a.Confidence,
            a.ErrorMessage,
            a.CreatedAt,
            a.CompletedAt);
}
