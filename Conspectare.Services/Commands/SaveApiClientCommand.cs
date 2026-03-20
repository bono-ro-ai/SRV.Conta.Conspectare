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
    protected override SaveApiClientResult OnExecute()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var hexChars = Convert.ToHexStringLower(randomBytes);
        var plainKey = $"csp_{hexChars}";
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
