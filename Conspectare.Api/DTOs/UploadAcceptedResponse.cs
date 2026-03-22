namespace Conspectare.Api.DTOs;

public record UploadAcceptedResponse(
    long Id,
    string DocumentRef,
    string Status,
    DateTime CreatedAt);
