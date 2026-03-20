namespace Conspectare.Api.DTOs;

public record CreateApiClientRequest(string Name, int RateLimitPerMin, int MaxFileSizeMb, string WebhookUrl);
public record CreateApiClientResponse(long Id, string Name, string ApiKeyPrefix, string ApiKey, DateTime CreatedAt);
public record ApiClientListItem(long Id, string Name, string ApiKeyPrefix, bool IsActive, bool IsAdmin, int RateLimitPerMin, int MaxFileSizeMb, string WebhookUrl, DateTime CreatedAt);
