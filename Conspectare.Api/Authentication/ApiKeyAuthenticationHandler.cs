using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NHibernate;
using ISession = NHibernate.ISession;
using ILoggerFactory = Microsoft.Extensions.Logging.ILoggerFactory;

namespace Conspectare.Api.Authentication;

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISessionFactory _sessionFactory;
    private readonly ITenantContext _tenantContext;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionFactory sessionFactory,
        ITenantContext tenantContext)
        : base(options, logger, encoder)
    {
        _sessionFactory = sessionFactory;
        _tenantContext = tenantContext;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var rawApiKey = authHeader["Bearer ".Length..].Trim();

        if (rawApiKey.Contains('.'))
        {
            return AuthenticateResult.NoResult();
        }

        if (rawApiKey.Length < 8)
        {
            return AuthenticateResult.Fail("Invalid API key format.");
        }

        var prefix = rawApiKey[..8];

        ApiClient apiClient;
        using (var session = _sessionFactory.OpenSession())
        {
            apiClient = await session.QueryOver<ApiClient>()
                .Where(x => x.ApiKeyPrefix == prefix)
                .SingleOrDefaultAsync<ApiClient>();
        }

        if (apiClient == null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
        var hashHex = Convert.ToHexStringLower(hashBytes);

        var storedHashBytes = Encoding.UTF8.GetBytes(apiClient.ApiKeyHash);
        var computedHashBytes = Encoding.UTF8.GetBytes(hashHex);

        if (!CryptographicOperations.FixedTimeEquals(computedHashBytes, storedHashBytes))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        if (!apiClient.IsActive)
        {
            return AuthenticateResult.Fail("API client is inactive.");
        }

        _tenantContext.TenantId = apiClient.Id;
        _tenantContext.ApiKeyPrefix = apiClient.ApiKeyPrefix;
        _tenantContext.RateLimitPerMin = apiClient.RateLimitPerMin;
        _tenantContext.IsAdmin = apiClient.IsAdmin;

        var claims = new[]
        {
            new Claim("tenantId", apiClient.Id.ToString()),
            new Claim("apiKeyPrefix", apiClient.ApiKeyPrefix),
            new Claim("rateLimitPerMin", apiClient.RateLimitPerMin.ToString()),
            new Claim("maxFileSizeMb", apiClient.MaxFileSizeMb.ToString()),
            new Claim("isAdmin", apiClient.IsAdmin.ToString())
        };

        var identity = new ClaimsIdentity(claims, AuthSchemeConstants.ApiKey);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthSchemeConstants.ApiKey);

        return AuthenticateResult.Success(ticket);
    }
}
