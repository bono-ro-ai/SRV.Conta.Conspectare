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

/// <summary>
/// Extension point for registering all LLM infrastructure services into the DI container.
/// Supports runtime provider selection (Claude or Gemini) via the "Llm:Provider" config key,
/// and always registers both concrete clients so the multi-model pipeline can use them directly.
/// </summary>
public static class LlmDependencyInjection
{
    /// <summary>
    /// Registers LLM API clients, metrics, and multi-model services.
    /// The active <see cref="ILlmApiClient"/> implementation is chosen based on
    /// the "Llm:Provider" configuration value ("claude" or "gemini"; defaults to "claude").
    /// Both <see cref="ClaudeApiClient"/> and <see cref="GeminiApiClient"/> are also registered
    /// as named typed clients so the consensus/multi-model pipeline can resolve them independently.
    /// </summary>
    public static void RegisterLlmServices(IConfiguration config, IServiceCollection services)
    {
        // Register OpenTelemetry metrics with a Prometheus scrape endpoint.
        services.AddOpenTelemetry()
            .WithMetrics(b => b
                .AddMeter(ConspectareMetrics.MeterName)
                .AddPrometheusExporter());

        // Wire up the primary ILlmApiClient based on the configured provider.
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

        // Always bind both settings sections and register both concrete clients so
        // LlmClientFactory can resolve whichever provider is requested at runtime.
        services.Configure<ClaudeApiSettings>(config.GetSection("Claude"));
        services.Configure<GeminiApiSettings>(config.GetSection("Gemini"));
        services.AddHttpClient<ClaudeApiClient>();
        services.AddHttpClient<GeminiApiClient>();

        // Build a keyed dictionary of clients and expose it through the factory interface.
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
