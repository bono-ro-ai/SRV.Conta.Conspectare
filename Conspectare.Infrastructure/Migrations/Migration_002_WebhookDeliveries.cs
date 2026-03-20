using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(2, "Add pipe_webhook_deliveries table for webhook notification queue")]
public class Migration_002_WebhookDeliveries : Migration
{
    public override void Up()
    {
        Create.Table("pipe_webhook_deliveries")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_webhook_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("webhook_url").AsString(500).NotNullable()
            .WithColumn("payload_json").AsCustom("LONGTEXT").NotNullable()
            .WithColumn("status").AsString(30).NotNullable()
            .WithColumn("http_status_code").AsInt32().Nullable()
            .WithColumn("error_message").AsString(2000).Nullable()
            .WithColumn("attempt_count").AsInt32().NotNullable()
            .WithColumn("max_attempts").AsInt32().NotNullable()
            .WithColumn("next_attempt_at").AsDateTime().Nullable()
            .WithColumn("last_attempt_at").AsDateTime().Nullable()
            .WithColumn("delivered_at").AsDateTime().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("idx_webhook_status_next").OnTable("pipe_webhook_deliveries")
            .OnColumn("status").Ascending()
            .OnColumn("next_attempt_at").Ascending();

        Create.Index("idx_webhook_doc").OnTable("pipe_webhook_deliveries")
            .OnColumn("document_id").Ascending();
    }

    public override void Down()
    {
        Delete.Table("pipe_webhook_deliveries");
    }
}
