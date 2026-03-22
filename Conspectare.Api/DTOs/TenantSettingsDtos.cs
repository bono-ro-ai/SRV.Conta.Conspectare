namespace Conspectare.Api.DTOs;

public record TenantSettingsResponse(
    long TenantId,
    string CompanyName,
    string Cui,
    string ContactEmail,
    string WebhookUrl,
    bool HasWebhookSecret,
    string ApiKeyPrefix,
    DateTime? TrialExpiresAt,
    bool IsTrialActive);

public record UpdateTenantSettingsRequest(
    string CompanyName,
    string Cui,
    string WebhookUrl,
    string WebhookSecret);

public record RotateApiKeyResponse(string ApiKey, string ApiKeyPrefix);
