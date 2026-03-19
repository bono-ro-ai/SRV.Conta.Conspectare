using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class DocumentArtifactMap : ClassMap<DocumentArtifact>
{
    public DocumentArtifactMap()
    {
        Table("pipe_document_artifacts");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.ArtifactType).Column("artifact_type").Not.Nullable();
        Map(x => x.S3Key).Column("s3_key").Not.Nullable();
        Map(x => x.ContentType).Column("content_type").Not.Nullable();
        Map(x => x.SizeBytes).Column("size_bytes");
        Map(x => x.RetentionDays).Column("retention_days").Not.Nullable();
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();

        References(x => x.Document).Column("document_id").Not.Nullable();
    }
}
