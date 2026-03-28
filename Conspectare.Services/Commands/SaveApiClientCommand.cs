using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public record SaveApiClientResult(ApiClient ApiClient, string PlainKey);

public class SaveApiClientCommand(
    string name,
    int rateLimitPerMin,
    int maxFileSizeMb,
    string webhookUrl)
    : NHibernateConspectareCommand<SaveApiClientResult>
{
    /// <summary>
    /// Generates a cryptographically random API key, stores only its SHA-256 hash
    /// and 8-character prefix in the database, saves the new <see cref="ApiClient"/>
    /// record, and returns both the persisted entity and the plaintext key (which is
    /// never stored and must be shown to the caller exactly once).
    /// </summary>
    protected override SaveApiClientResult OnExecute()
    {
        // Generate 32 random bytes → 64-char hex string prefixed with "csp_".
        // Only the hash is persisted; the plain key is returned for one-time display.
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";

        // Short prefix stored in plain text to allow lookup without full key comparison.
        var prefix = plainKey[..8];
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        var hashHex = Convert.ToHexStringLower(hash);

        var now = DateTime.UtcNow;

        var apiClient = new ApiClient
        {
            Name = name,
            ApiKeyHash = hashHex,
            ApiKeyPrefix = prefix,
            IsActive = true,
            IsAdmin = false,
            RateLimitPerMin = rateLimitPerMin,
            MaxFileSizeMb = maxFileSizeMb,
            WebhookUrl = webhookUrl,
            CreatedAt = now,
            UpdatedAt = now
        };

        Session.Save(apiClient);

        return new SaveApiClientResult(apiClient, plainKey);
    }
}
