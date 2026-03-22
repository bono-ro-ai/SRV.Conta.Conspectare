using System.Text;
using ISession = NHibernate.ISession;
using Conspectare.Api.Authentication;
using Conspectare.Infrastructure.Llm.Claude;
using Conspectare.Infrastructure.Llm.Gemini;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services.Infrastructure;
using Conspectare.Infrastructure.Mappings;
using Conspectare.Infrastructure.Migrations;
using Conspectare.Services.Core.Database;
using Conspectare.Services;
using Conspectare.Services.Configuration;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Processors;
using Conspectare.Services.Observability;
using Conspectare.Workers;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;

namespace Conspectare.Api.Configuration;

internal static class DependencyInjection
{
    internal static void RegisterAppServices(IConfiguration config, IServiceCollection services)
    {
        ConfigurationValidator.Validate(config);

        var connectionString = config.GetConnectionString("ConspectareDb")!;

        var nhSection = config.GetSection("NHibernate");
        var showSql = nhSection.GetValue<bool>("ShowSql");
        var formatSql = nhSection.GetValue<bool>("FormatSql");

        NHibernateConspectare.Configure<ApiClientMap>(
            connectionString,
            showSql,
            formatSql);

        services.AddSingleton(NHibernateConspectare.SessionFactory);
        services.AddScoped<ISession>(sp => sp.GetRequiredService<NHibernate.ISessionFactory>().OpenSession());

        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddMySql5()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(Migration_001_Baseline).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        services.AddScoped<ITenantContext, TenantContext>();

        services.Configure<JwtSettings>(config.GetSection("Jwt"));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();

        ConfigureAuthentication(config, services);

        services.Configure<AwsSettings>(config.GetSection("Aws"));
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<IDistributedLock, MariaDbDistributedLock>();

        services.AddSingleton<ConspectareMetrics>();
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

        services.Configure<AnafVatValidationSettings>(config.GetSection("Anaf"));
        services.AddHttpClient<IAnafVatValidationClient, AnafVatValidationClient>();
        services.AddScoped<VatValidationService>();

        services.AddSingleton<IPromptService, PromptService>();
        services.AddSingleton<IPipelineSignal, PipelineSignal>();
        services.AddSingleton<DocumentStatusWorkflow>();
        services.AddScoped<IDocumentProcessor, EFacturaXmlProcessor>();
        services.AddScoped<IDocumentProcessor, ImageDocumentProcessor>();
        services.AddScoped<IDocumentProcessor, PdfDocumentProcessor>();
        services.AddScoped<IProcessorRegistry, ProcessorRegistry>();
        services.AddSingleton<IDocumentRefAllocator, DocumentRefAllocator>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IReviewService, ReviewService>();

        services.AddHttpClient<IWebhookDispatchService, WebhookDispatchService>();

        services.AddHostedService<TriageWorker>();
        services.AddHostedService<ExtractionWorker>();
        services.AddHostedService<WebhookWorker>();
        services.AddHostedService<VatRetryWorker>();
        services.AddHostedService<StaleClaimRecoveryWorker>();
        services.AddHostedService<UsageAggregationWorker>();
        services.AddHostedService<AuditCleanupWorker>();
    }

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
                    return Task.CompletedTask;
                }
            };
        })
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthSchemeConstants.ApiKey, null);

        services.AddAuthorization();
    }
}
