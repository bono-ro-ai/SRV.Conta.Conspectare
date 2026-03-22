using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Queries;

namespace Conspectare.Services.Commands;

public class UpsertUsageDailyCommand(long tenantId, DateTime usageDate, UsageAggregateResult aggregate)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var existing = Session.QueryOver<UsageDaily>()
            .Where(u => u.TenantId == tenantId)
            .And(u => u.UsageDate == usageDate.Date)
            .SingleOrDefault();

        if (existing != null)
        {
            existing.DocumentsIngested = aggregate.DocumentsIngested;
            existing.DocumentsProcessed = aggregate.DocumentsProcessed;
            existing.LlmInputTokens = aggregate.LlmInputTokens;
            existing.LlmOutputTokens = aggregate.LlmOutputTokens;
            existing.LlmRequests = aggregate.LlmRequests;
            existing.StorageBytes = aggregate.StorageBytes;
            existing.ApiCalls = aggregate.ApiCalls;
            existing.UpdatedAt = DateTime.UtcNow;
            Session.Update(existing);
        }
        else
        {
            var entity = new UsageDaily
            {
                TenantId = tenantId,
                UsageDate = usageDate.Date,
                DocumentsIngested = aggregate.DocumentsIngested,
                DocumentsProcessed = aggregate.DocumentsProcessed,
                LlmInputTokens = aggregate.LlmInputTokens,
                LlmOutputTokens = aggregate.LlmOutputTokens,
                LlmRequests = aggregate.LlmRequests,
                StorageBytes = aggregate.StorageBytes,
                ApiCalls = aggregate.ApiCalls,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            Session.Save(entity);
        }
    }
}
