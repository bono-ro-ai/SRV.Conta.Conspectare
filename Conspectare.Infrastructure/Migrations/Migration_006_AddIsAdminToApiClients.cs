using FluentMigrator;
namespace Conspectare.Infrastructure.Migrations;
[Migration(6, "Add is_admin column to cfg_api_clients")]
public class Migration_006_AddIsAdminToApiClients : Migration
{
    public override void Up()
    {
        Alter.Table("cfg_api_clients")
            .AddColumn("is_admin").AsBoolean().NotNullable().WithDefaultValue(false);
    }
    public override void Down()
    {
        Delete.Column("is_admin").FromTable("cfg_api_clients");
    }
}
