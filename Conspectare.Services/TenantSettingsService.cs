using System.Security.Cryptography;
using System.Text;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;

namespace Conspectare.Services;

/// <summary>
/// Manages tenant-level settings and API key lifecycle for the authenticated tenant.
/// All operations are scoped to the current request's <see cref="ITenantContext"/>.
/// </summary>
public class TenantSettingsService : ITenantSettingsService
{
    private readonly ITenantContext _tenant;

    public TenantSettingsService(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    /// <summary>
    /// Retrieves the settings for the current tenant.
    /// Returns <see cref="OperationResult{T}.NotFound"/> if the tenant's API client record does not exist.
    /// </summary>
    public Task<OperationResult<TenantSettings>> GetSettingsAsync()
    {
        var apiClient = new LoadApiClientByIdQuery(_tenant.TenantId).Execute();
        if (apiClient == null)
            return Task.FromResult(OperationResult<TenantSettings>.NotFound("Tenant not found."));

        var settings = MapToSettings(apiClient);
        return Task.FromResult(OperationResult<TenantSettings>.Success(settings));
    }

    /// <summary>
    /// Applies the non-null fields from <paramref name="input"/> to the tenant's API client record
    /// and persists the changes. Strips the "RO" prefix from CUI values before saving.
    /// Returns <see cref="OperationResult{T}.NotFound"/> if the tenant record does not exist.
    /// </summary>
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
            // Normalise by stripping the Romanian VAT prefix before storing.
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

    /// <summary>
    /// Generates a new API key for the current tenant, stores its SHA-256 hash and 8-character
    /// prefix, and returns the full plain-text key (shown once — never stored).
    /// Returns <see cref="OperationResult{T}.NotFound"/> if the tenant record does not exist.
    /// </summary>
    public Task<OperationResult<RotateApiKeyResult>> RotateApiKeyAsync()
    {
        var apiClient = new LoadApiClientByIdQuery(_tenant.TenantId).Execute();
        if (apiClient == null)
            return Task.FromResult(OperationResult<RotateApiKeyResult>.NotFound("Tenant not found."));

        // Generate a 32-byte random key and encode it as "csp_<hex>".
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";

        // Store only the prefix (for display) and the hash (for verification).
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

    /// <summary>
    /// Projects an <see cref="Domain.Entities.ApiClient"/> entity onto the public-facing <see cref="TenantSettings"/> DTO.
    /// The <c>HasWebhookSecret</c> flag is derived rather than exposing the secret value itself.
    /// </summary>
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
