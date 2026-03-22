using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Filters;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class DocumentMap : ClassMap<Document>
{
    public DocumentMap()
    {
        Table("pipe_documents");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.ExternalRef).Column("external_ref");
        Map(x => x.ContentHash).Column("content_hash");
        Map(x => x.FileName).Column("file_name").Not.Nullable();
        Map(x => x.ContentType).Column("content_type").Not.Nullable();
        Map(x => x.FileSizeBytes).Column("file_size_bytes").Not.Nullable();
        Map(x => x.InputFormat).Column("input_format").Not.Nullable();
        Map(x => x.Status).Column("status").Not.Nullable();
        Map(x => x.DocumentType).Column("document_type");
        Map(x => x.TriageConfidence).Column("triage_confidence").Precision(5).Scale(4);
        Map(x => x.IsAccountingRelevant).Column("is_accounting_relevant");
        Map(x => x.RetryCount).Column("retry_count").Not.Nullable();
        Map(x => x.MaxRetries).Column("max_retries").Not.Nullable();
        Map(x => x.ErrorMessage).Column("error_message").Length(2000);
        Map(x => x.RawFileS3Key).Column("raw_file_s3_key").Not.Nullable();
        Map(x => x.DocumentRef).Column("document_ref");
        Map(x => x.FiscalCode).Column("fiscal_code");
        Map(x => x.ClientReference).Column("client_reference");
        Map(x => x.Metadata).Column("metadata").CustomSqlType("LONGTEXT");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();
        Map(x => x.CompletedAt).Column("completed_at");

        References(x => x.Tenant).Column("tenant_id").Not.Nullable().Not.Insert().Not.Update();

        HasMany(x => x.Artifacts).KeyColumn("document_id").Inverse().Cascade.AllDeleteOrphan();
        HasMany(x => x.ExtractionAttempts).KeyColumn("document_id").Inverse().Cascade.AllDeleteOrphan();
        HasMany(x => x.ReviewFlags).KeyColumn("document_id").Inverse().Cascade.AllDeleteOrphan();
        HasMany(x => x.Events).KeyColumn("document_id").Inverse().Cascade.AllDeleteOrphan();

        HasOne(x => x.CanonicalOutput).PropertyRef(nameof(CanonicalOutput.Document)).Cascade.All();

        ApplyFilter<TenantFilterDefinition>("tenant_id = :tenantId");
    }
}
