using Conspectare.Domain.Entities;

namespace Conspectare.Services.Interfaces;

public record TenantSettings(
    long TenantId,
    string CompanyName,
    string Cui,
    string ContactEmail,
    string WebhookUrl,
    bool HasWebhookSecret,
    string ApiKeyPrefix,
    DateTime? TrialExpiresAt,
    bool IsTrialActive);

public record UpdateTenantSettingsInput(
    string CompanyName,
    string Cui,
    string WebhookUrl,
    string WebhookSecret);

public record RotateApiKeyResult(string PlainApiKey, string ApiKeyPrefix);

public interface ITenantSettingsService
{
    Task<OperationResult<TenantSettings>> GetSettingsAsync();
    Task<OperationResult<TenantSettings>> UpdateSettingsAsync(UpdateTenantSettingsInput input);
    Task<OperationResult<RotateApiKeyResult>> RotateApiKeyAsync();
}
