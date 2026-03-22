using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(12, "Widen supplier_cui and customer_cui columns to support international VAT numbers")]
public class Migration_012_WidenCuiColumns : Migration
{
    public override void Up()
    {
        Alter.Table("pipe_canonical_outputs").AlterColumn("supplier_cui").AsString(50).Nullable();
        Alter.Table("pipe_canonical_outputs").AlterColumn("customer_cui").AsString(50).Nullable();
    }

    public override void Down()
    {
        Alter.Table("pipe_canonical_outputs").AlterColumn("supplier_cui").AsString(15).Nullable();
        Alter.Table("pipe_canonical_outputs").AlterColumn("customer_cui").AsString(15).Nullable();
    }
}
