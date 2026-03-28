using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindErrorRatesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<ErrorRatesResult>
{
    // Both terminal failure statuses are treated as errors for rate calculation.
    private static readonly string[] FailedStatuses =
    {
        DocumentStatus.Failed,
        DocumentStatus.ExtractionFailed
    };

    /// <summary>
    /// Returns the total document count and the failed document count for the specified tenant
    /// within the given date range, enabling error-rate calculation by the caller.
    /// </summary>
    protected override ErrorRatesResult OnExecute()
    {
        var total = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CreatedAt >= from)
            .And(d => d.CreatedAt <= to)
            .RowCount();

        var failed = Session.QueryOver<Document>()
            .Where(d => d.TenantId == tenantId)
            .And(d => d.CreatedAt >= from)
            .And(d => d.CreatedAt <= to)
            .AndRestrictionOn(d => d.Status).IsIn(FailedStatuses)
            .RowCount();

        return new ErrorRatesResult(total, failed);
    }
}

public class ErrorRatesResult
{
    public int Total { get; }
    public int Failed { get; }

    public ErrorRatesResult(int total, int failed)
    {
        Total = total;
        Failed = failed;
    }
}
