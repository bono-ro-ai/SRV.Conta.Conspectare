using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Filters;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class ExtractionAttemptMap : ClassMap<ExtractionAttempt>
{
    public ExtractionAttemptMap()
    {
        Table("pipe_extraction_attempts");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.AttemptNumber).Column("attempt_number").Not.Nullable();
        Map(x => x.Phase).Column("phase").Not.Nullable();
        Map(x => x.ModelId).Column("model_id").Not.Nullable();
        Map(x => x.PromptVersion).Column("prompt_version").Not.Nullable();
        Map(x => x.Status).Column("status").Not.Nullable();
        Map(x => x.InputTokens).Column("input_tokens");
        Map(x => x.OutputTokens).Column("output_tokens");
        Map(x => x.LatencyMs).Column("latency_ms");
        Map(x => x.Confidence).Column("confidence").Precision(5).Scale(4);
        Map(x => x.ErrorMessage).Column("error_message").Length(2000);
        Map(x => x.ResponseArtifactId).Column("response_artifact_id");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.CompletedAt).Column("completed_at");
        Map(x => x.ProviderKey).Column("provider_key");

        Map(x => x.DocumentId).Column("document_id").Not.Insert().Not.Update();
        References(x => x.Document).Column("document_id").Not.Nullable();

        ApplyFilter<TenantFilterDefinition>("tenant_id = :tenantId");
    }
}
