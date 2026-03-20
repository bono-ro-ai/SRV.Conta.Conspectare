using ISession = NHibernate.ISession;
using Conspectare.Infrastructure.Llm.Claude;
using Conspectare.Infrastructure.Llm.Gemini;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services.Infrastructure;
using Conspectare.Infrastructure.Mappings;
using Conspectare.Infrastructure.Migrations;
using Conspectare.Services.Core.Database;
using Conspectare.Services;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Processors;
using Conspectare.Workers;
using FluentMigrator.Runner;

namespace Conspectare.Api.Configuration;

internal static class DependencyInjection
{
    internal static void RegisterAppServices(IConfiguration config, IServiceCollection services)
    {
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

        services.Configure<AwsSettings>(config.GetSection("Aws"));
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<IDistributedLock, MariaDbDistributedLock>();

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

        services.Configure<AnafVatValidationSettings>(config.GetSection("Anaf"));
        services.AddHttpClient<IAnafVatValidationClient, AnafVatValidationClient>();
        services.AddScoped<VatValidationService>();

        services.AddSingleton<DocumentStatusWorkflow>();
        services.AddScoped<IDocumentProcessor, EFacturaXmlProcessor>();
        services.AddScoped<IDocumentProcessor, ImageDocumentProcessor>();
        services.AddScoped<IDocumentProcessor, PdfDocumentProcessor>();
        services.AddScoped<IProcessorRegistry, ProcessorRegistry>();
        services.AddScoped<IDocumentService, DocumentService>();

        services.AddHostedService<TriageWorker>();
        services.AddHostedService<ExtractionWorker>();
    }
}
