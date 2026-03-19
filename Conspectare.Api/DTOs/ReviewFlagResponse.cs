using Conspectare.Domain.Entities;

namespace Conspectare.Api.DTOs;

public record ReviewFlagResponse(
    long Id,
    string FlagType,
    string Severity,
    string Message,
    bool IsResolved,
    DateTime? ResolvedAt,
    DateTime CreatedAt)
{
    public static ReviewFlagResponse FromEntity(ReviewFlag flag) =>
        new(
            flag.Id,
            flag.FlagType,
            flag.Severity,
            flag.Message,
            flag.IsResolved,
            flag.ResolvedAt,
            flag.CreatedAt);
}
