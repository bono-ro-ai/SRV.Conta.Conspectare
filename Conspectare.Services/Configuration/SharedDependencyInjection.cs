using ISession = NHibernate.ISession;
using Conspectare.Infrastructure.Settings;
using Conspectare.Infrastructure.Mappings;
using Conspectare.Services;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Configuration;
using Conspectare.Services.Extraction;
using Conspectare.Services.Infrastructure;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Processors;
using Conspectare.Services.Observability;
using Conspectare.Services.Triage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;

namespace Conspectare.Services.Configuration;

public static class SharedDependencyInjection
{
    public static void RegisterSharedServices(IConfiguration config, IServiceCollection services)
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
        services.AddScoped<ISession>(sp => sp.GetRequiredService<global::NHibernate.ISessionFactory>().OpenSession());

        services.Configure<AwsSettings>(config.GetSection("Aws"));
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<ICanonicalOutputJsonService, CanonicalOutputJsonService>();
        services.AddSingleton<IDistributedLock, MariaDbDistributedLock>();

        services.AddSingleton<ConspectareMetrics>();

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

        services.AddScoped<ExtractionOrchestrationService>();
        services.AddScoped<TriageOrchestrationService>();
    }
}
