using FluentMigrator;
namespace Conspectare.Infrastructure.Migrations;
[Migration(8, "Add webhook secret and seed P&L ApiClient")]
public class Migration_008_SeedPnlApiClient : Migration
{
    public override void Up()
    {
        Alter.Table("cfg_api_clients")
            .AddColumn("webhook_secret").AsString(128).Nullable();
        var pnlApiUrl = Environment.GetEnvironmentVariable("PNL_API_URL") ?? "http://localhost:5200";
        var webhookUrl = $"{pnlApiUrl}/api/v1/webhooks/conspectare";
        var webhookSecret = Environment.GetEnvironmentVariable("PNL_WEBHOOK_SECRET") ?? "whsec_dev_placeholder";
        Insert.IntoTable("cfg_api_clients").Row(new
        {
            name = "P&L Expense Tracker",
            api_key_hash = "98878f43563fda8c14ea05c299b39fc2021f7abac257daa9b55158fe124f88cc",
            api_key_prefix = "csp_pnl_",
            webhook_url = webhookUrl,
            webhook_secret = webhookSecret,
            is_active = true,
            is_admin = false,
            rate_limit_per_min = 60,
            max_file_size_mb = 10,
            created_at = SystemMethods.CurrentUTCDateTime,
            updated_at = SystemMethods.CurrentUTCDateTime
        });
    }
    public override void Down()
    {
        Delete.FromTable("cfg_api_clients").Row(new { name = "P&L Expense Tracker" });
        Delete.Column("webhook_secret").FromTable("cfg_api_clients");
    }
}
