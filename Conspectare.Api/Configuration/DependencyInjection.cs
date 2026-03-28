using System.Text;
using ISession = NHibernate.ISession;
using Conspectare.Api.Authentication;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services;
using Conspectare.Services.Auth;
using Conspectare.Services.Configuration;
using Conspectare.Infrastructure.Llm.Configuration;
using Conspectare.Services.Email;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Conspectare.Api.Configuration;

/// <summary>
/// Wires up application-level services and authentication schemes.
/// Kept internal so that service registration is not accessible from outside this assembly.
/// </summary>
internal static class DependencyInjection
{
    /// <summary>
    /// Registers all application services, settings, and authentication configuration
    /// into <paramref name="services"/>.
    /// Delegates shared and LLM-specific registrations to their own DI modules.
    /// </summary>
    internal static void RegisterAppServices(IConfiguration config, IServiceCollection services)
    {
        SharedDependencyInjection.RegisterSharedServices(config, services);
        LlmDependencyInjection.RegisterLlmServices(config, services);

        services.AddScoped<ITenantContext, TenantContext>();

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.Configure<GoogleAuthSettings>(config.GetSection("Google"));
        services.Configure<MandrillSettings>(config.GetSection("Mandrill"));
        services.Configure<AppSettings>(config.GetSection("App"));

        services.AddHttpClient<IEmailService, MandrillEmailService>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton<IGoogleGroupChecker, GoogleGroupChecker>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();

        ConfigureAuthentication(config, services);
    }

    /// <summary>
    /// Registers a dual-scheme authentication setup that transparently routes requests to
    /// either the JWT Bearer handler or the API Key handler based on the shape of the token:
    /// tokens containing a dot are treated as JWTs; all other tokens are treated as API keys.
    /// </summary>
    private static void ConfigureAuthentication(IConfiguration config, IServiceCollection services)
    {
        var jwtSection = config.GetSection("Jwt");
        var secret = jwtSection.GetValue<string>("Secret") ?? string.Empty;
        var issuer = jwtSection.GetValue<string>("Issuer") ?? "conspectare-api";
        var audience = jwtSection.GetValue<string>("Audience") ?? "conspectare-dashboard";

        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
            throw new InvalidOperationException("JWT Secret must be at least 32 characters");

        services.AddAuthentication(options =>
        {
            options.DefaultScheme = AuthSchemeConstants.DualAuth;
            options.DefaultChallengeScheme = AuthSchemeConstants.DualAuth;
        })
        .AddPolicyScheme(AuthSchemeConstants.DualAuth, AuthSchemeConstants.DualAuth, options =>
        {
            // Inspect the Authorization header and forward to the appropriate scheme:
            // a token with dots is a three-part JWT; anything else is an opaque API key.
            options.ForwardDefaultSelector = context =>
            {
                var authHeader = context.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
                {
                    var token = authHeader["Bearer ".Length..].Trim();
                    if (token.Contains('.'))
                    {
                        return AuthSchemeConstants.JwtBearer;
                    }
                }
                return AuthSchemeConstants.ApiKey;
            };
        })
        .AddJwtBearer(AuthSchemeConstants.JwtBearer, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                // Allow a 30-second clock skew to tolerate minor drift between servers.
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();

                    var roleClaim = context.Principal?.FindFirst("role")?.Value;
                    if (roleClaim == "admin")
                        tenantContext.IsAdmin = true;

                    var tenantIdClaim = context.Principal?.FindFirst("tenantId")?.Value;
                    if (tenantIdClaim != null && long.TryParse(tenantIdClaim, out var tenantId))
                        tenantContext.TenantId = tenantId;

                    // An explicit X-Tenant-Id header lets an admin user impersonate another tenant.
                    var xTenant = context.HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                    if (xTenant != null && long.TryParse(xTenant, out var headerTenantId))
                        tenantContext.TenantId = headerTenantId;

                    // Fall back to the Dashboard Admin tenant (id = 2) for JWT users with no tenant claim.
                    if (tenantContext.TenantId == 0)
                        tenantContext.TenantId = 2;

                    var emailClaim = context.Principal?.FindFirst("email")?.Value;
                    if (!string.IsNullOrEmpty(emailClaim))
                        tenantContext.UserIdentity = emailClaim;

                    return Task.CompletedTask;
                }
            };
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthSchemeConstants.ApiKey, null);

        services.AddAuthorization();
    }
}
