using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindProcessingTimesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<ProcessingTimesResult>
{
    protected override ProcessingTimesResult OnExecute()
    {
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

        var values = latencies.Where(v => v.HasValue).Select(v => v.Value).ToList();

        if (values.Count == 0)
            return new ProcessingTimesResult(null, null, 0);

        var p50 = Percentile(values, 0.50);
        var p95 = Percentile(values, 0.95);

        return new ProcessingTimesResult(p50, p95, values.Count);
    }

    private static double Percentile(List<int> sorted, double percentile)
    {
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
