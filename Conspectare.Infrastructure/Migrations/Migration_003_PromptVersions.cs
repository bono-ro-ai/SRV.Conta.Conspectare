using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(3, "Add cfg_prompt_versions table for DB-stored prompt management with A/B testing")]
public class Migration_003_PromptVersions : Migration
{
    public override void Up()
    {
        Create.Table("cfg_prompt_versions")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("phase").AsString(50).NotNullable()
            .WithColumn("document_type").AsString(50).Nullable()
            .WithColumn("version").AsString(100).NotNullable()
            .WithColumn("prompt_text").AsCustom("LONGTEXT").NotNullable()
            .WithColumn("is_active").AsBoolean().NotNullable()
            .WithColumn("traffic_weight").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("idx_prompt_versions_phase_type_active").OnTable("cfg_prompt_versions")
            .OnColumn("phase").Ascending()
            .OnColumn("document_type").Ascending()
            .OnColumn("is_active").Ascending();
    }

    public override void Down()
    {
        Delete.Table("cfg_prompt_versions");
    }
}
