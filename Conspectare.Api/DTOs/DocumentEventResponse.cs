using Conspectare.Domain.Entities;

namespace Conspectare.Api.DTOs;

public record DocumentEventResponse(
    long Id,
    string EventType,
    string FromStatus,
    string ToStatus,
    string Details,
    DateTime CreatedAt)
{
    public static DocumentEventResponse FromEntity(DocumentEvent e) =>
        new(
            e.Id,
            e.EventType,
            e.FromStatus,
            e.ToStatus,
            e.Details,
            e.CreatedAt);
}
