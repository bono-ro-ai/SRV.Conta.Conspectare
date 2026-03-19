using Conspectare.Api.Middleware;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

namespace Conspectare.Api.Configuration;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddExceptionHandler<Middleware.GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();
        services.AddControllers();
        services.AddOpenApi();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedHost |
                                       ForwardedHeaders.XForwardedFor |
                                       ForwardedHeaders.XForwardedProto;
        });

        DependencyInjection.RegisterAppServices(configuration, services);
    }

    public static void Configure(WebApplication app, IWebHostEnvironment env)
    {
        using var scope = app.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        runner.MigrateUp();

        app.UseExceptionHandler();
        app.UseForwardedHeaders();
        app.UseRouting();

        if (env.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        app.UseMiddleware<ApiKeyAuthMiddleware>();
        app.UseMiddleware<TenantMiddleware>();
        app.UseMiddleware<RateLimitingMiddleware>();

        app.MapControllers();
    }
}
