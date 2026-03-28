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

/// <summary>
/// Background worker that processes documents in the Triage stage of the pipeline.
/// Claims up to <see cref="BatchSize"/> pending documents per run, routes each through
/// <see cref="TriageOrchestrationService"/>, and records per-document metrics.
/// </summary>
public class TriageWorker : DistributedBackgroundService
{
    private const int BatchSize = 5;

    protected override string JobName => "triage_worker";
    protected override TimeSpan Interval => TimeSpan.FromSeconds(3);
    protected override string SignalStage => PipelinePhase.Triage;

    /// <summary>
    /// Initialises the worker and wires it to the pipeline signal so it can wake up
    /// immediately when new documents enter the Triage stage.
    /// </summary>
    public TriageWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<TriageWorker> logger,
        IPipelineSignal signal)
        : base(distributedLock, scopeFactory, logger, signal)
    {
    }

    /// <summary>
    /// Finds and claims a batch of pending triage documents, then orchestrates each one.
    /// Returns the number of documents successfully processed.
    /// </summary>
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var orchestration = scope.ServiceProvider.GetRequiredService<TriageOrchestrationService>();
        var metrics = scope.ServiceProvider.GetRequiredService<ConspectareMetrics>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TriageWorker>>();

        var pendingDocs = new FindPendingTriageDocumentsQuery(BatchSize).Execute();
        if (pendingDocs.Count == 0)
            return 0;

        // Claiming is a separate step to mark documents as in-progress before processing,
        // which prevents other instances from picking up the same batch.
        var claimedDocs = new ClaimDocumentsForTriageCommand(pendingDocs).Execute();
        if (claimedDocs.Count == 0)
            return 0;

        // Record ingestion metrics for every claimed document before processing starts.
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
                logger.LogError(ex, "TriageWorker: failed to triage document {DocumentId}", doc.Id);
            }
        }

        return processedCount;
    }
}
