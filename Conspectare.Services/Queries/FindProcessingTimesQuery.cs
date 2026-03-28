using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindProcessingTimesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<ProcessingTimesResult>
{
    /// <summary>
    /// Returns P50 and P95 extraction latency percentiles (in milliseconds) for the specified
    /// tenant and date range. Only completed attempts with a recorded latency are included.
    /// Percentiles are computed in-process over an ordered sample of up to 10,000 rows.
    /// Returns a result with null percentiles when no qualifying data is available.
    /// </summary>
    protected override ProcessingTimesResult OnExecute()
    {
        // Fetch latency values pre-sorted ascending so the in-process percentile calculation
        // can index directly without a second sort pass. Capped at 10,000 for memory safety.
        var latencies = Session.QueryOver<ExtractionAttempt>()
            .Where(a => a.TenantId == tenantId)
            .And(a => a.Status == ExtractionAttemptStatus.Completed)
            .And(a => a.CreatedAt >= from)
            .And(a => a.CreatedAt <= to)
            .And(a => a.LatencyMs != null)
            .Select(a => a.LatencyMs)
            .OrderBy(a => a.LatencyMs).Asc
            .Take(10000)
            .List<int?>();

        // Strip nullable wrappers; the query filter already excludes nulls but the projection
        // returns int? so we normalize here.
        var values = latencies.Where(v => v.HasValue).Select(v => v.Value).ToList();

        if (values.Count == 0)
            return new ProcessingTimesResult(null, null, 0);

        var p50 = Percentile(values, 0.50);
        var p95 = Percentile(values, 0.95);

        return new ProcessingTimesResult(p50, p95, values.Count);
    }

    /// <summary>
    /// Computes the value at the given percentile from an already-sorted list using
    /// the nearest-rank method (ceiling-based index). The list must be sorted ascending.
    /// </summary>
    private static double Percentile(List<int> sorted, double percentile)
    {
        // Ceiling-based nearest-rank: index = ceil(p * N) - 1, clamped to [0, N-1].
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, index)];
    }
}

public class ProcessingTimesResult
{
    public double? P50 { get; }
    public double? P95 { get; }
    public int SampleCount { get; }

    public ProcessingTimesResult(double? p50, double? p95, int sampleCount)
    {
        P50 = p50;
        P95 = p95;
        SampleCount = sampleCount;
    }
}
