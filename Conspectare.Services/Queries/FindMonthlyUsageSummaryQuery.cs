using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindMonthlyUsageSummaryQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<IList<MonthlyUsageSummary>>
{
    /// <summary>
    /// Returns usage metrics aggregated by calendar month for the specified tenant and date range.
    /// Daily rows are fetched from the database and then grouped in-process to produce monthly totals,
    /// avoiding a database-specific date-truncation function.
    /// </summary>
    protected override IList<MonthlyUsageSummary> OnExecute()
    {
        // Load all daily snapshots within the range; grouping is done in-process below.
        var dailyRows = Session.QueryOver<UsageDaily>()
            .Where(u => u.TenantId == tenantId)
            .And(u => u.UsageDate >= from.Date)
            .And(u => u.UsageDate <= to.Date)
            .List();

        // Group by year+month and sum every metric across the constituent days.
        return dailyRows
            .GroupBy(u => new { u.UsageDate.Year, u.UsageDate.Month })
            .Select(g => new MonthlyUsageSummary
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                DocumentsIngested = g.Sum(u => u.DocumentsIngested),
                DocumentsProcessed = g.Sum(u => u.DocumentsProcessed),
                LlmInputTokens = g.Sum(u => u.LlmInputTokens),
                LlmOutputTokens = g.Sum(u => u.LlmOutputTokens),
                LlmRequests = g.Sum(u => u.LlmRequests),
                StorageBytes = g.Sum(u => u.StorageBytes),
                ApiCalls = g.Sum(u => u.ApiCalls)
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();
    }
}

public class MonthlyUsageSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int DocumentsIngested { get; set; }
    public int DocumentsProcessed { get; set; }
    public long LlmInputTokens { get; set; }
    public long LlmOutputTokens { get; set; }
    public int LlmRequests { get; set; }
    public long StorageBytes { get; set; }
    public int ApiCalls { get; set; }
}
