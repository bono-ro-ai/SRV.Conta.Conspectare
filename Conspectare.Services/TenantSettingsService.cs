using System.Security.Cryptography;
using System.Text;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;

namespace Conspectare.Services;

public class TenantSettingsService : ITenantSettingsService
{
    private readonly ITenantContext _tenant;

    public TenantSettingsService(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<OperationResult<TenantSettings>> GetSettingsAsync()
    {
        var apiClient = new LoadApiClientByIdQuery(_tenant.TenantId).Execute();
        if (apiClient == null)
            return Task.FromResult(OperationResult<TenantSettings>.NotFound("Tenant not found."));

        var settings = MapToSettings(apiClient);
        return Task.FromResult(OperationResult<TenantSettings>.Success(settings));
    }

    public Task<OperationResult<TenantSettings>> UpdateSettingsAsync(UpdateTenantSettingsInput input)
    {
        var apiClient = new LoadApiClientByIdQuery(_tenant.TenantId).Execute();
        if (apiClient == null)
            return Task.FromResult(OperationResult<TenantSettings>.NotFound("Tenant not found."));

        if (input.CompanyName != null)
        {
            apiClient.CompanyName = input.CompanyName;
            apiClient.Name = input.CompanyName;
        }

        if (input.Cui != null)
        {
            var normalized = input.Cui.Trim();
            if (normalized.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[2..];
            apiClient.Cui = normalized;
        }

        if (input.WebhookUrl != null)
            apiClient.WebhookUrl = input.WebhookUrl;

        if (input.WebhookSecret != null)
            apiClient.WebhookSecret = input.WebhookSecret;

        apiClient.UpdatedAt = DateTime.UtcNow;
        SaveOrUpdateCommand.For(apiClient).Execute();

        var settings = MapToSettings(apiClient);
        return Task.FromResult(OperationResult<TenantSettings>.Success(settings));
    }

    public Task<OperationResult<RotateApiKeyResult>> RotateApiKeyAsync()
    {
        var apiClient = new LoadApiClientByIdQuery(_tenant.TenantId).Execute();
        if (apiClient == null)
            return Task.FromResult(OperationResult<RotateApiKeyResult>.NotFound("Tenant not found."));

        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";
        var prefix = plainKey[..8];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        var hashHex = Convert.ToHexStringLower(hash);

        apiClient.ApiKeyHash = hashHex;
        apiClient.ApiKeyPrefix = prefix;
        apiClient.UpdatedAt = DateTime.UtcNow;
        SaveOrUpdateCommand.For(apiClient).Execute();

        return Task.FromResult(OperationResult<RotateApiKeyResult>.Success(
            new RotateApiKeyResult(plainKey, prefix)));
    }

    private static TenantSettings MapToSettings(Domain.Entities.ApiClient apiClient)
    {
        return new TenantSettings(
            apiClient.Id,
            apiClient.CompanyName,
            apiClient.Cui,
            apiClient.ContactEmail,
            apiClient.WebhookUrl,
            !string.IsNullOrEmpty(apiClient.WebhookSecret),
            apiClient.ApiKeyPrefix,
            apiClient.TrialExpiresAt,
            apiClient.TrialExpiresAt.HasValue && apiClient.TrialExpiresAt.Value > DateTime.UtcNow);
    }
}
