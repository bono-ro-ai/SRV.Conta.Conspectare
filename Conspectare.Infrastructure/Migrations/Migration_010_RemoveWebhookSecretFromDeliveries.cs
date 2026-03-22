using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(10, "Remove webhook_secret column from pipe_webhook_deliveries")]
public class Migration_010_RemoveWebhookSecretFromDeliveries : Migration
{
    public override void Up()
    {
        Delete.Column("webhook_secret").FromTable("pipe_webhook_deliveries");
    }
    public override void Down()
    {
        Alter.Table("pipe_webhook_deliveries").AddColumn("webhook_secret").AsString(128).Nullable();
    }
}
