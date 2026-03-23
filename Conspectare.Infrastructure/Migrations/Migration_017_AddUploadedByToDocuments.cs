using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(17, "Add uploaded_by column to pipe_documents")]
public class Migration_017_AddUploadedByToDocuments : Migration
{
    public override void Up()
    {
        Alter.Table("pipe_documents")
            .AddColumn("uploaded_by").AsString(254).Nullable();
    }

    public override void Down()
    {
        Delete.Column("uploaded_by").FromTable("pipe_documents");
    }
}
