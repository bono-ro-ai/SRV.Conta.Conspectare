using System.Diagnostics;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Commands;
using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Observability;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class ExtractionWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;
    protected override string JobName => "extraction_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    protected override string SignalStage => PipelinePhase.Extraction;
    public ExtractionWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<ExtractionWorker> logger,
        IPipelineSignal signal)
        : base(distributedLock, scopeFactory, logger, signal)
    {
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var orchestration = scope.ServiceProvider.GetRequiredService<ExtractionOrchestrationService>();
        var metrics = scope.ServiceProvider.GetRequiredService<ConspectareMetrics>();
        var workflow = scope.ServiceProvider.GetRequiredService<DocumentStatusWorkflow>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExtractionWorker>>();
        var pendingDocs = new FindPendingExtractionDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;
        var claimedDocs = new ClaimDocumentsForExtractionCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;
        var processedCount = 0;
        foreach (var doc in claimedDocs)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            try
            {
                if (orchestration.IsMultiModelEnabled)
                    await orchestration.ProcessDocumentMultiModelAsync(doc, ct);
                else
                    await orchestration.ProcessDocumentAsync(doc, ct);
                sw.Stop();
                metrics.RecordProcessingDuration(PipelinePhase.Extraction, sw.ElapsedMilliseconds);
                processedCount++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                sw.Stop();
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.RecordProcessingDuration(PipelinePhase.Extraction, sw.ElapsedMilliseconds);
                ExtractionErrorHandler.Handle(doc, workflow, metrics, ex, DateTime.UtcNow, logger);
            }
        }
        return processedCount;
    }
}
