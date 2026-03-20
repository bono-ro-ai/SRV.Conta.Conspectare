using FluentMigrator;
namespace Conspectare.Infrastructure.Migrations;
[Migration(5, "Align extraction schema with conta requirements: rename totals, add new fields")]
public class Migration_005_AddExtractionFields : Migration
{
    public override void Up()
    {
        Rename.Column("total_amount").OnTable("pipe_canonical_outputs").To("tax_inclusive_amount");
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("tax_exclusive_amount").AsDecimal(18, 4).Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("discount").AsDecimal(18, 4).Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("tax_note").AsCustom("TEXT").Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("tax_category").AsString(50).Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("swift_bic").AsString(11).Nullable();
    }
    public override void Down()
    {
        Delete.Column("swift_bic").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_category").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_note").FromTable("pipe_canonical_outputs");
        Delete.Column("discount").FromTable("pipe_canonical_outputs");
        Delete.Column("tax_exclusive_amount").FromTable("pipe_canonical_outputs");
        Rename.Column("tax_inclusive_amount").OnTable("pipe_canonical_outputs").To("total_amount");
    }
}
