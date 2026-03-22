using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(16, "Add sec_magic_link_tokens table and make sec_users.password_hash nullable")]
public class Migration_016_MagicLinkTokens : Migration
{
    public override void Up()
    {
        Create.Table("sec_magic_link_tokens")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("user_id").AsInt64().Nullable()
            .WithColumn("token_hash").AsString(255).NotNullable()
            .WithColumn("email").AsString(255).NotNullable()
            .WithColumn("expires_at").AsDateTime().NotNullable()
            .WithColumn("used_at").AsDateTime().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("ip_address").AsString(45).Nullable();

        Create.Index("ux_sec_magic_link_tokens_hash")
            .OnTable("sec_magic_link_tokens")
            .OnColumn("token_hash").Ascending()
            .WithOptions().Unique();

        Create.Index("ix_sec_magic_link_tokens_user_id")
            .OnTable("sec_magic_link_tokens")
            .OnColumn("user_id").Ascending();

        Alter.Column("password_hash").OnTable("sec_users").AsString(255).Nullable();
    }

    public override void Down()
    {
        Delete.Table("sec_magic_link_tokens");
        Alter.Column("password_hash").OnTable("sec_users").AsString(255).NotNullable();
    }
}
