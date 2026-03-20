using FluentMigrator;
namespace Conspectare.Infrastructure.Migrations;
[Migration(4, "Add multi-model consensus columns to extraction attempts and canonical outputs")]
public class Migration_004_MultiModelConsensus : Migration
{
    public override void Up()
    {
        Alter.Table("pipe_extraction_attempts")
            .AddColumn("provider_key").AsString(50).Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("consensus_strategy").AsString(50).Nullable();
        Alter.Table("pipe_canonical_outputs")
            .AddColumn("winning_model_id").AsString(100).Nullable();
    }
    public override void Down()
    {
        Delete.Column("provider_key").FromTable("pipe_extraction_attempts");
        Delete.Column("consensus_strategy").FromTable("pipe_canonical_outputs");
        Delete.Column("winning_model_id").FromTable("pipe_canonical_outputs");
    }
}
