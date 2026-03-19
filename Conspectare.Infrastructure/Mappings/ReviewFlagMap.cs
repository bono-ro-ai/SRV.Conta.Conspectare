using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class ReviewFlagMap : ClassMap<ReviewFlag>
{
    public ReviewFlagMap()
    {
        Table("pipe_review_flags");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.FlagType).Column("flag_type").Not.Nullable();
        Map(x => x.Severity).Column("severity").Not.Nullable();
        Map(x => x.Message).Column("message").Not.Nullable();
        Map(x => x.IsResolved).Column("is_resolved").Not.Nullable();
        Map(x => x.ResolvedAt).Column("resolved_at");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();

        Map(x => x.DocumentId).Column("document_id").Not.Insert().Not.Update();
        References(x => x.Document).Column("document_id").Not.Nullable();
    }
}
