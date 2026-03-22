using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(14, "Create audit_usage_daily table")]
public class Migration_014_UsageDaily : Migration
{
    public override void Up()
    {
        Create.Table("audit_usage_daily")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("usage_date").AsDate().NotNullable()
            .WithColumn("documents_ingested").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("documents_processed").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("llm_input_tokens").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("llm_output_tokens").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("llm_requests").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("storage_bytes").AsInt64().NotNullable().WithDefaultValue(0)
            .WithColumn("api_calls").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("uq_audit_usage_daily_tenant_date")
            .OnTable("audit_usage_daily")
            .OnColumn("tenant_id").Ascending()
            .OnColumn("usage_date").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Table("audit_usage_daily");
    }
}
