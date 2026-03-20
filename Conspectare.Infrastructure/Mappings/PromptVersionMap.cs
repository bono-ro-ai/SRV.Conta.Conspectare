using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class PromptVersionMap : ClassMap<PromptVersion>
{
    public PromptVersionMap()
    {
        Table("cfg_prompt_versions");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.Phase).Column("phase").Not.Nullable().Length(50);
        Map(x => x.DocumentType).Column("document_type").Nullable().Length(50);
        Map(x => x.Version).Column("version").Not.Nullable().Length(100);
        Map(x => x.PromptText).Column("prompt_text").Not.Nullable().CustomSqlType("LONGTEXT");
        Map(x => x.IsActive).Column("is_active").Not.Nullable();
        Map(x => x.TrafficWeight).Column("traffic_weight").Not.Nullable();
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();
    }
}
