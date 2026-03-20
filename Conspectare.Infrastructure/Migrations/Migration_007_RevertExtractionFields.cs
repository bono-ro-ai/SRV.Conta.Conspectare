using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(7, "Revert extraction field changes from migration 005")]
public class Migration_007_RevertExtractionFields : Migration
{
    public override void Up()
    {
        Delete.Column("discount").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_note").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_category").FromTable("pipe_canonical_outputs");
        Delete.Column("swift_bic").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_exclusive_amount").FromTable("pipe_canonical_outputs");

        Rename.Column("tax_inclusive_amount").OnTable("pipe_canonical_outputs").To("total_amount");
    }

    public override void Down()
    {
        Rename.Column("total_amount").OnTable("pipe_canonical_outputs").To("tax_inclusive_amount");

        Alter.Table("pipe_canonical_outputs")
            .AddColumn("tax_exclusive_amount").AsDecimal(18, 4).Nullable()
            .AddColumn("discount").AsDecimal(18, 4).Nullable()
            .AddColumn("tax_note").AsCustom("TEXT").Nullable()
            .AddColumn("tax_category").AsString(50).Nullable()
            .AddColumn("swift_bic").AsString(11).Nullable();
    }
}
