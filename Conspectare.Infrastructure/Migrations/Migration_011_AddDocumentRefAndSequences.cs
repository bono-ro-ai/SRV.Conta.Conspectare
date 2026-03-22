using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(11, "Add document_ref and fiscal_code to pipe_documents; create cfg_document_ref_sequences")]
public class Migration_011_AddDocumentRefAndSequences : Migration
{
    public override void Up()
    {
        Alter.Table("pipe_documents").AddColumn("document_ref").AsString(50).Nullable();
        Alter.Table("pipe_documents").AddColumn("fiscal_code").AsString(20).Nullable();

        Create.UniqueConstraint("uq_pipe_documents_document_ref")
            .OnTable("pipe_documents").Column("document_ref");

        Create.Table("cfg_document_ref_sequences")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("fiscal_code").AsString(20).NotNullable()
            .WithColumn("year").AsInt32().NotNullable()
            .WithColumn("last_seq").AsInt32().NotNullable();

        Create.UniqueConstraint("uq_cfg_document_ref_sequences_fc_year")
            .OnTable("cfg_document_ref_sequences").Columns("fiscal_code", "year");
    }

    public override void Down()
    {
        Delete.Table("cfg_document_ref_sequences");

        Delete.UniqueConstraint("uq_pipe_documents_document_ref").FromTable("pipe_documents");
        Delete.Column("fiscal_code").FromTable("pipe_documents");
        Delete.Column("document_ref").FromTable("pipe_documents");
    }
}
