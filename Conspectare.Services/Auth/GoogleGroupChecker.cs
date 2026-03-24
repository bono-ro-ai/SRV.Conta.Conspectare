using System.Text.Json;
using Conspectare.Services.Configuration;
using Google.Apis.Admin.Directory.directory_v1;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Conspectare.Services.Auth;

public interface IGoogleGroupChecker
{
    Task<bool> IsMemberAsync(string email);
}

public class GoogleGroupChecker : IGoogleGroupChecker
{
    private readonly GoogleAuthSettings _settings;
    private readonly ILogger<GoogleGroupChecker> _logger;

    public GoogleGroupChecker(IOptions<GoogleAuthSettings> options, ILogger<GoogleGroupChecker> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsMemberAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(_settings.AllowedGroup))
            return true;

        if (string.IsNullOrWhiteSpace(_settings.ServiceAccountJson))
        {
            _logger.LogWarning("Google Group check skipped — ServiceAccountJson not configured");
            return true;
        }

        try
        {
            var credential = GoogleCredential
                .FromJson(_settings.ServiceAccountJson)
                .CreateScoped("https://www.googleapis.com/auth/admin.directory.group.member.readonly")
                .CreateWithUser(_settings.AdminEmail);

            using var service = new DirectoryService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Conspectare"
            });

            var request = service.Members.HasMember(_settings.AllowedGroup, email);
            var result = await request.ExecuteAsync();
            return result.IsMember ?? false;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google Group membership check failed for {Email}", email);
            return true;
        }
    }
}

public class NoOpGoogleGroupChecker : IGoogleGroupChecker
{
    public Task<bool> IsMemberAsync(string email) => Task.FromResult(true);
}
