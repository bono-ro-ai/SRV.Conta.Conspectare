using ISession = NHibernate.ISession;
using Conspectare.Infrastructure.Settings;
using Conspectare.Services.Infrastructure;
using Conspectare.Infrastructure.Mappings;
using Conspectare.Infrastructure.Migrations;
using Conspectare.Services.Core.Database;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Processors;
using Conspectare.Services.Workers;
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

        services.Configure<ClaudeApiSettings>(config.GetSection("Claude"));
        services.AddHttpClient<IClaudeApiClient, Conspectare.Services.Infrastructure.ClaudeApiClient>();

        services.AddSingleton<DocumentStatusWorkflow>();
        services.AddScoped<IDocumentProcessor, EFacturaXmlProcessor>();
        services.AddScoped<IProcessorRegistry, ProcessorRegistry>();
        services.AddScoped<IDocumentService, DocumentService>();

        services.AddHostedService<TriageWorker>();
        services.AddHostedService<ExtractionWorker>();
    }
}
