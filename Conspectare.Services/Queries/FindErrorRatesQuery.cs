using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindErrorRatesQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<ErrorRatesResult>
{
    private static readonly string[] FailedStatuses =
    {
        DocumentStatus.Failed,
        DocumentStatus.ExtractionFailed
    };

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
