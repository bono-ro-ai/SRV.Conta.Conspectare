using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(18, "Add output_json_s3_key and make output_json nullable")]
public class Migration_018_MoveOutputJsonToS3 : Migration
{
    public override void Up()
    {
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("output_json_s3_key").AsString(512).Nullable();

        Alter.Column("output_json").OnTable("pipe_canonical_outputs")
            .AsCustom("LONGTEXT").Nullable();
    }

    public override void Down()
    {
        Delete.Column("output_json_s3_key").FromTable("pipe_canonical_outputs");

        Alter.Column("output_json").OnTable("pipe_canonical_outputs")
            .AsCustom("LONGTEXT").NotNullable();
    }
}
