using FluentMigrator;
namespace Conspectare.Infrastructure.Migrations;
[Migration(8, "Add webhook secret and seed P&L ApiClient")]
public class Migration_008_SeedPnlApiClient : Migration
{
    public override void Up()
    {
        if (!Schema.Table("cfg_api_clients").Column("webhook_secret").Exists())
            Alter.Table("cfg_api_clients")
                .AddColumn("webhook_secret").AsString(128).Nullable();
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        if (env == "Development")
        {
            var pnlApiUrl = Environment.GetEnvironmentVariable("PNL_API_URL") ?? "http://localhost:5200";
            var webhookUrl = $"{pnlApiUrl}/api/v1/webhooks/conspectare";
            var webhookSecret = Environment.GetEnvironmentVariable("PNL_WEBHOOK_SECRET") ?? "whsec_dev_placeholder";
            Execute.Sql($@"INSERT INTO cfg_api_clients (name, api_key_hash, api_key_prefix, webhook_url, webhook_secret, is_active, is_admin, rate_limit_per_min, max_file_size_mb, created_at, updated_at)
                SELECT 'P&L Expense Tracker', '98878f43563fda8c14ea05c299b39fc2021f7abac257daa9b55158fe124f88cc', 'csp_pnl_', '{webhookUrl}', '{webhookSecret}', 1, 0, 60, 10, UTC_TIMESTAMP(), UTC_TIMESTAMP()
                FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM cfg_api_clients WHERE api_key_prefix = 'csp_pnl_')");
        }
    }
    public override void Down()
    {
        Execute.Sql("DELETE FROM cfg_api_clients WHERE api_key_prefix = 'csp_pnl_'");
    }
}
