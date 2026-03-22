using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class UsageDailyMap : ClassMap<UsageDaily>
{
    public UsageDailyMap()
    {
        Table("audit_usage_daily");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.UsageDate).Column("usage_date").Not.Nullable();
        Map(x => x.DocumentsIngested).Column("documents_ingested").Not.Nullable();
        Map(x => x.DocumentsProcessed).Column("documents_processed").Not.Nullable();
        Map(x => x.LlmInputTokens).Column("llm_input_tokens").Not.Nullable();
        Map(x => x.LlmOutputTokens).Column("llm_output_tokens").Not.Nullable();
        Map(x => x.LlmRequests).Column("llm_requests").Not.Nullable();
        Map(x => x.StorageBytes).Column("storage_bytes").Not.Nullable();
        Map(x => x.ApiCalls).Column("api_calls").Not.Nullable();
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();
    }
}
