using Conspectare.Infrastructure.Llm.Configuration;
using Conspectare.Services.Configuration;
using Conspectare.Workers;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.AddJsonConsole();

SharedDependencyInjection.RegisterSharedServices(builder.Configuration, builder.Services);
LlmDependencyInjection.RegisterLlmServices(builder.Configuration, builder.Services);

builder.Services.AddHealthChecks();

builder.Services.AddHostedService<TriageWorker>();
builder.Services.AddHostedService<ExtractionWorker>();
builder.Services.AddHostedService<WebhookWorker>();
builder.Services.AddHostedService<VatRetryWorker>();
builder.Services.AddHostedService<StaleClaimRecoveryWorker>();
builder.Services.AddHostedService<UsageAggregationWorker>();
builder.Services.AddHostedService<AuditCleanupWorker>();

var app = builder.Build();

app.Urls.Add("http://+:5101");
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
