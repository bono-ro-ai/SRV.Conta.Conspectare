using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(18, "Add Google OAuth fields to sec_users")]
public class Migration_018_GoogleOAuth : Migration
{
    public override void Up()
    {
        Alter.Table("sec_users")
            .AddColumn("google_id").AsString(255).Nullable()
            .AddColumn("avatar_url").AsString(512).Nullable();

        Alter.Column("password_hash").OnTable("sec_users").AsString(255).Nullable();

        Create.Index("ux_sec_users_google_id")
            .OnTable("sec_users")
            .OnColumn("google_id")
            .Unique();
    }

    public override void Down()
    {
        Delete.Index("ux_sec_users_google_id").OnTable("sec_users");
        Delete.Column("google_id").FromTable("sec_users");
        Delete.Column("avatar_url").FromTable("sec_users");

        Alter.Column("password_hash").OnTable("sec_users").AsString(255).NotNullable();
    }
}
