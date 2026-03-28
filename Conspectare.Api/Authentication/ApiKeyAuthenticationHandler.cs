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

/// <summary>
/// Handles authentication for requests that present a plain API key as a Bearer token.
/// Looks up the client by the first 8-character prefix, then verifies the full key using
/// a constant-time SHA-256 hash comparison to prevent timing attacks.
/// On success, populates <see cref="ITenantContext"/> so downstream middleware and controllers
/// have access to tenant metadata without re-querying the database.
/// </summary>
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

    /// <summary>
    /// Attempts to authenticate the request using an API key extracted from the
    /// <c>Authorization: Bearer &lt;key&gt;</c> header.
    /// Returns <see cref="AuthenticateResult.NoResult"/> when the header is absent or
    /// the token looks like a JWT (contains a dot), deferring to the JWT handler.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var rawApiKey = authHeader["Bearer ".Length..].Trim();

        // A dot in the token indicates a JWT; let the JWT handler take over.
        if (rawApiKey.Contains('.'))
        {
            return AuthenticateResult.NoResult();
        }

        if (rawApiKey.Length < 8)
        {
            return AuthenticateResult.Fail("Invalid API key format.");
        }

        // The first 8 characters are the publicly visible prefix used to locate the record.
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

        // Hash the supplied key and compare using FixedTimeEquals to prevent timing side-channels.
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

        // Populate the ambient tenant context so the rest of the pipeline can read it
        // without issuing additional database queries.
        _tenantContext.TenantId = apiClient.Id;
        _tenantContext.ApiKeyPrefix = apiClient.ApiKeyPrefix;
        _tenantContext.RateLimitPerMin = apiClient.RateLimitPerMin;
        _tenantContext.IsAdmin = apiClient.IsAdmin;
        _tenantContext.UserIdentity = $"api:{apiClient.Name}";

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
