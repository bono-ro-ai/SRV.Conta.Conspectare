using Conspectare.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

namespace Conspectare.Api.Configuration;

/// <summary>
/// Bootstraps the ASP.NET Core service container and HTTP pipeline.
/// Separated from <c>Program.cs</c> to keep startup logic testable and easy to navigate.
/// </summary>
public static class Startup
{
    /// <summary>
    /// Registers all services required by the application into the DI container.
    /// CORS origins are read from the <c>Cors:AllowedOrigins</c> configuration section;
    /// when none are configured, all origins are allowed (useful in development).
    /// </summary>
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddExceptionHandler<Middleware.GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins);
                else
                    policy.AllowAnyOrigin();

                policy.AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        services.AddControllers();
        services.AddOpenApi();

        // Trust X-Forwarded-* headers from the Railway reverse proxy so that
        // client IP addresses and HTTPS scheme are resolved correctly.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedHost |
                                       ForwardedHeaders.XForwardedFor |
                                       ForwardedHeaders.XForwardedProto;
        });

        DependencyInjection.RegisterAppServices(configuration, services);
    }

    /// <summary>
    /// Configures the middleware pipeline in the correct order.
    /// OpenAPI/Scalar endpoints are only exposed in the Development environment.
    /// </summary>
    public static void Configure(WebApplication app, IWebHostEnvironment env)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseCors();

        if (env.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // TenantMiddleware must run after authentication so TenantContext is populated.
        app.UseMiddleware<TenantMiddleware>();
        app.UseMiddleware<RateLimitingMiddleware>();

        app.MapControllers();
        app.MapPrometheusScrapingEndpoint();
    }
}
