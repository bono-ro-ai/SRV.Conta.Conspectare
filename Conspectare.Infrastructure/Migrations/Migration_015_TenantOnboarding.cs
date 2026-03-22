using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(15, "Add tenant onboarding columns to cfg_api_clients and sec_users")]
public class Migration_015_TenantOnboarding : Migration
{
    public override void Up()
    {
        Alter.Table("cfg_api_clients")
            .AddColumn("company_name").AsString(255).Nullable()
            .AddColumn("cui").AsString(20).Nullable()
            .AddColumn("contact_email").AsString(255).Nullable()
            .AddColumn("trial_expires_at").AsDateTime().Nullable();

        Alter.Table("sec_users")
            .AddColumn("tenant_id").AsInt64().Nullable();

        Create.Index("ix_sec_users_tenant_id")
            .OnTable("sec_users")
            .OnColumn("tenant_id").Ascending();
    }

    public override void Down()
    {
        Delete.Index("ix_sec_users_tenant_id").OnTable("sec_users");
        Delete.Column("tenant_id").FromTable("sec_users");
        Delete.Column("company_name").FromTable("cfg_api_clients");
        Delete.Column("cui").FromTable("cfg_api_clients");
        Delete.Column("contact_email").FromTable("cfg_api_clients");
        Delete.Column("trial_expires_at").FromTable("cfg_api_clients");
    }
}
