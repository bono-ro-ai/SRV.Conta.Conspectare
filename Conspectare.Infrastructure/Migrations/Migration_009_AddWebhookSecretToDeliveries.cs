using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(9, "Add webhook_secret column to cfg_api_clients and pipe_webhook_deliveries")]
public class Migration_009_AddWebhookSecretToDeliveries : Migration
{
    public override void Up()
    {
        if (!Schema.Table("cfg_api_clients").Column("webhook_secret").Exists())
            Alter.Table("cfg_api_clients").AddColumn("webhook_secret").AsString(128).Nullable();
        Alter.Table("pipe_webhook_deliveries").AddColumn("webhook_secret").AsString(128).Nullable();
    }
    public override void Down()
    {
        Delete.Column("webhook_secret").FromTable("pipe_webhook_deliveries");
        if (Schema.Table("cfg_api_clients").Column("webhook_secret").Exists())
            Delete.Column("webhook_secret").FromTable("cfg_api_clients");
    }
}
