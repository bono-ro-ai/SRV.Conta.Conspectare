namespace Conspectare.Api.DTOs;

public record UploadAcceptedResponse(
    long Id,
    string Status,
    DateTime CreatedAt);
