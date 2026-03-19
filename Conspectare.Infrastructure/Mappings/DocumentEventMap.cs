using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Filters;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class DocumentEventMap : ClassMap<DocumentEvent>
{
    public DocumentEventMap()
    {
        Table("pipe_document_events");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.EventType).Column("event_type").Not.Nullable();
        Map(x => x.FromStatus).Column("from_status");
        Map(x => x.ToStatus).Column("to_status");
        Map(x => x.Details).Column("details").CustomSqlType("LONGTEXT");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();

        Map(x => x.DocumentId).Column("document_id").Not.Insert().Not.Update();
        References(x => x.Document).Column("document_id").Not.Nullable();

        ApplyFilter<TenantFilterDefinition>("tenant_id = :tenantId");
    }
}
