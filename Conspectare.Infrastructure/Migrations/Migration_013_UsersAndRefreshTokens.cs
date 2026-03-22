using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(13, "Create sec_users and sec_refresh_tokens tables")]
public class Migration_013_UsersAndRefreshTokens : Migration
{
    public override void Up()
    {
        Create.Table("sec_users")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("email").AsString(255).NotNullable().Unique()
            .WithColumn("name").AsString(255).NotNullable()
            .WithColumn("password_hash").AsString(255).NotNullable()
            .WithColumn("role").AsString(50).NotNullable().WithDefaultValue("user")
            .WithColumn("is_active").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("failed_login_attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("locked_until").AsDateTime().Nullable()
            .WithColumn("last_login_at").AsDateTime().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Table("sec_refresh_tokens")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt64().NotNullable().ForeignKey("fk_refresh_tokens_user", "sec_users", "id")
            .WithColumn("token_hash").AsString(128).NotNullable().Unique()
            .WithColumn("expires_at").AsDateTime().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("revoked_at").AsDateTime().Nullable()
            .WithColumn("replaced_by_token_id").AsInt64().Nullable();

        Create.Index("ix_sec_refresh_tokens_user_id")
            .OnTable("sec_refresh_tokens")
            .OnColumn("user_id");
    }

    public override void Down()
    {
        Delete.Table("sec_refresh_tokens");
        Delete.Table("sec_users");
    }
}
