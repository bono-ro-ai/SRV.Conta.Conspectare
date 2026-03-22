using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class AggregateUsageForTenantQuery(long tenantId, DateTime dateUtc)
    : NHibernateConspectareQuery<UsageAggregateResult>
{
    protected override UsageAggregateResult OnExecute()
    {
        var dayStart = dateUtc.Date;
        var dayEnd = dayStart.AddDays(1);

        var documentsIngested = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CreatedAt >= dayStart)
            .And(d => d.CreatedAt < dayEnd)
            .RowCount();

        var documentsProcessed = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CompletedAt >= dayStart)
            .And(d => d.CompletedAt < dayEnd)
            .RowCount();

        ExtractionTokenResult tokenResult = null;
        var tokenRows = Session.QueryOver<ExtractionAttempt>()
            .Where(a => a.TenantId == tenantId)
            .And(a => a.CreatedAt >= dayStart)
            .And(a => a.CreatedAt < dayEnd)
            .SelectList(list => list
                .SelectSum(a => a.InputTokens).WithAlias(() => tokenResult.TotalInputTokens)
                .SelectSum(a => a.OutputTokens).WithAlias(() => tokenResult.TotalOutputTokens)
                .SelectCount(a => a.Id).WithAlias(() => tokenResult.RequestCount))
            .TransformUsing(NHibernate.Transform.Transformers.AliasToBean<ExtractionTokenResult>())
            .List<ExtractionTokenResult>();
        var tokens = tokenRows.FirstOrDefault() ?? new ExtractionTokenResult();

        var storageBytes = Session.QueryOver<DocumentArtifact>()
            .Where(a => a.TenantId == tenantId)
            .And(a => a.CreatedAt >= dayStart)
            .And(a => a.CreatedAt < dayEnd)
            .Select(Projections.Sum<DocumentArtifact>(a => a.SizeBytes))
            .SingleOrDefault<long?>();

        return new UsageAggregateResult
        {
            DocumentsIngested = documentsIngested,
            DocumentsProcessed = documentsProcessed,
            LlmInputTokens = tokens.TotalInputTokens ?? 0,
            LlmOutputTokens = tokens.TotalOutputTokens ?? 0,
            LlmRequests = tokens.RequestCount,
            StorageBytes = storageBytes ?? 0,
            // v1: counts document ingestion calls only; accurate per-request counting requires middleware-level tracking
            ApiCalls = documentsIngested
        };
    }
}

public class UsageAggregateResult
{
    public int DocumentsIngested { get; set; }
    public int DocumentsProcessed { get; set; }
    public long LlmInputTokens { get; set; }
    public long LlmOutputTokens { get; set; }
    public int LlmRequests { get; set; }
    public long StorageBytes { get; set; }
    public int ApiCalls { get; set; }
}

public class ExtractionTokenResult
{
    public int? TotalInputTokens { get; set; }
    public int? TotalOutputTokens { get; set; }
    public int RequestCount { get; set; }
}
