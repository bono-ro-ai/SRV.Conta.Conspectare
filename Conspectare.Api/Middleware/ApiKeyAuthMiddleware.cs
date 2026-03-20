using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using NHibernate;
using NHibernate.Criterion;
using ISession = NHibernate.ISession;

namespace Conspectare.Api.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] ExemptPrefixes = ["/health", "/swagger", "/scalar"];

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISessionFactory sessionFactory, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (ExemptPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or malformed Authorization header.");
            return;
        }

        var rawApiKey = authHeader["Bearer ".Length..].Trim();

        if (rawApiKey.Length < 8)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid API key format.");
            return;
        }

        var prefix = rawApiKey[..8];

        ApiClient apiClient;
        using (var session = sessionFactory.OpenSession())
        {
            apiClient = await session.QueryOver<ApiClient>()
                .Where(x => x.ApiKeyPrefix == prefix)
                .SingleOrDefaultAsync<ApiClient>();
        }

        if (apiClient == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid API key.");
            return;
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
        var hashHex = Convert.ToHexStringLower(hashBytes);

        var storedHashBytes = Encoding.UTF8.GetBytes(apiClient.ApiKeyHash);
        var computedHashBytes = Encoding.UTF8.GetBytes(hashHex);

        if (!CryptographicOperations.FixedTimeEquals(computedHashBytes, storedHashBytes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid API key.");
            return;
        }

        if (!apiClient.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("API client is inactive.");
            return;
        }

        tenantContext.TenantId = apiClient.Id;
        tenantContext.ApiKeyPrefix = apiClient.ApiKeyPrefix;
        tenantContext.RateLimitPerMin = apiClient.RateLimitPerMin;
        tenantContext.IsAdmin = apiClient.IsAdmin;

        await _next(context);
    }
}
