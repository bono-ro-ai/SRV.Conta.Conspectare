using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

namespace Conspectare.Api.Configuration;

public static class Startup
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
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
        app.UseForwardedHeaders();
        app.UseRouting();

        if (env.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        app.MapControllers();
    }
}
