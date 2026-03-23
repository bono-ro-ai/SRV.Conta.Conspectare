using System.Diagnostics;
using Conspectare.Domain.Enums;
using Conspectare.Services.Commands;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Observability;
using Conspectare.Services.Queries;
using Conspectare.Services.Triage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class TriageWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;
    protected override string JobName => "triage_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    protected override string SignalStage => PipelinePhase.Triage;
    public TriageWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<TriageWorker> logger,
        IPipelineSignal signal)
        : base(distributedLock, scopeFactory, logger, signal)
    {
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var orchestration = scope.ServiceProvider.GetRequiredService<TriageOrchestrationService>();
        var metrics = scope.ServiceProvider.GetRequiredService<ConspectareMetrics>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TriageWorker>>();
        var pendingDocs = new FindPendingTriageDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;
        var claimedDocs = new ClaimDocumentsForTriageCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;
        foreach (var doc in claimedDocs)
            metrics.RecordDocumentIngested(doc.InputFormat ?? "unknown");
        var processedCount = 0;
        foreach (var doc in claimedDocs)
        {
            ct.ThrowIfCancellationRequested();
            var sw = Stopwatch.StartNew();
            try
            {
                await orchestration.ProcessDocumentAsync(doc, ct);
                sw.Stop();
                metrics.RecordProcessingDuration(PipelinePhase.Triage, sw.ElapsedMilliseconds);
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
                metrics.RecordProcessingDuration(PipelinePhase.Triage, sw.ElapsedMilliseconds);
                metrics.RecordDocumentFailed(PipelinePhase.Triage, "unhandled_error");
                logger.LogError(ex,
                    "TriageWorker: failed to triage document {DocumentId}", doc.Id);
            }
        }
        return processedCount;
    }
}
