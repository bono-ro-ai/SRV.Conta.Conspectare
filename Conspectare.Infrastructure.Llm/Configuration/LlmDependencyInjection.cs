using Conspectare.Infrastructure.Llm.Claude;
using Conspectare.Infrastructure.Llm.Gemini;
using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Conspectare.Infrastructure.Llm.Configuration;

public static class LlmDependencyInjection
{
    public static void RegisterLlmServices(IConfiguration config, IServiceCollection services)
    {
        services.AddOpenTelemetry()
            .WithMetrics(b => b
                .AddMeter(ConspectareMetrics.MeterName)
                .AddPrometheusExporter());

        var llmProvider = config.GetValue<string>("Llm:Provider") ?? "claude";
        switch (llmProvider.ToLowerInvariant())
        {
            case "gemini":
                services.Configure<GeminiApiSettings>(config.GetSection("Gemini"));
                services.AddHttpClient<ILlmApiClient, GeminiApiClient>();
                break;
            case "claude":
            default:
                services.Configure<ClaudeApiSettings>(config.GetSection("Claude"));
                services.AddHttpClient<ILlmApiClient, ClaudeApiClient>();
                break;
        }

        services.Configure<ClaudeApiSettings>(config.GetSection("Claude"));
        services.Configure<GeminiApiSettings>(config.GetSection("Gemini"));
        services.AddHttpClient<ClaudeApiClient>();
        services.AddHttpClient<GeminiApiClient>();
        services.AddSingleton<ILlmClientFactory>(sp =>
        {
            var clients = new Dictionary<string, ILlmApiClient>
            {
                ["claude"] = sp.GetRequiredService<ClaudeApiClient>(),
                ["gemini"] = sp.GetRequiredService<GeminiApiClient>()
            };
            return new LlmClientFactory(clients);
        });
        services.Configure<MultiModelSettings>(config.GetSection("Llm:MultiModel"));
        services.AddSingleton<IConsensusStrategy, HighestConfidenceStrategy>();
        services.AddScoped<MultiModelExtractionService>();
    }
}
