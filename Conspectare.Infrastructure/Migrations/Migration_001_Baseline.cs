using FluentMigrator;

namespace Conspectare.Infrastructure.Migrations;

[Migration(1, "Baseline schema: all 8 tables with indexes, FKs, and seed data")]
public class Migration_001_Baseline : Migration
{
    public override void Up()
    {
        // 1. cfg_api_clients (no FKs)
        Create.Table("cfg_api_clients")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("name").AsString(255).NotNullable()
            .WithColumn("api_key_hash").AsString(128).NotNullable()
            .WithColumn("api_key_prefix").AsString(8).NotNullable()
            .WithColumn("is_active").AsBoolean().NotNullable()
            .WithColumn("rate_limit_per_min").AsInt32().NotNullable()
            .WithColumn("max_file_size_mb").AsInt32().NotNullable()
            .WithColumn("webhook_url").AsString(500).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable();

        Create.Index("uq_api_key_prefix").OnTable("cfg_api_clients")
            .OnColumn("api_key_prefix").Unique();

        // 2. audit_job_executions (no FKs)
        Create.Table("audit_job_executions")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("job_name").AsString(100).NotNullable()
            .WithColumn("instance_id").AsString(100).NotNullable()
            .WithColumn("started_at").AsDateTime().NotNullable()
            .WithColumn("completed_at").AsDateTime().Nullable()
            .WithColumn("duration_ms").AsInt32().Nullable()
            .WithColumn("status").AsString(30).NotNullable()
            .WithColumn("items_processed").AsInt32().Nullable()
            .WithColumn("error_message").AsString(2000).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("idx_job_name_started").OnTable("audit_job_executions")
            .OnColumn("job_name").Ascending()
            .OnColumn("started_at").Ascending();

        // 3. pipe_documents (FK to cfg_api_clients)
        Create.Table("pipe_documents")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("tenant_id").AsInt64().NotNullable()
                .ForeignKey("fk_doc_tenant", "cfg_api_clients", "id")
            .WithColumn("external_ref").AsString(255).Nullable()
            .WithColumn("content_hash").AsString(64).Nullable()
            .WithColumn("file_name").AsString(500).NotNullable()
            .WithColumn("content_type").AsString(100).NotNullable()
            .WithColumn("file_size_bytes").AsInt64().NotNullable()
            .WithColumn("input_format").AsString(20).NotNullable()
            .WithColumn("status").AsString(30).NotNullable()
            .WithColumn("document_type").AsString(30).Nullable()
            .WithColumn("triage_confidence").AsDecimal(5, 4).Nullable()
            .WithColumn("is_accounting_relevant").AsBoolean().Nullable()
            .WithColumn("retry_count").AsInt32().NotNullable()
            .WithColumn("max_retries").AsInt32().NotNullable()
            .WithColumn("error_message").AsString(2000).Nullable()
            .WithColumn("raw_file_s3_key").AsString(500).NotNullable()
            .WithColumn("client_reference").AsString(255).Nullable()
            .WithColumn("metadata").AsCustom("LONGTEXT").Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("updated_at").AsDateTime().NotNullable()
            .WithColumn("completed_at").AsDateTime().Nullable();

        Create.Index("idx_doc_tenant_status").OnTable("pipe_documents")
            .OnColumn("tenant_id").Ascending()
            .OnColumn("status").Ascending();

        Create.Index("idx_doc_status").OnTable("pipe_documents")
            .OnColumn("status").Ascending();

        Create.Index("idx_doc_tenant_created").OnTable("pipe_documents")
            .OnColumn("tenant_id").Ascending()
            .OnColumn("created_at").Descending();

        Create.Index("idx_doc_tenant_extref").OnTable("pipe_documents")
            .OnColumn("tenant_id").Ascending()
            .OnColumn("external_ref").Ascending();

        // 4. pipe_document_artifacts (FK to pipe_documents)
        Create.Table("pipe_document_artifacts")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_artifact_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("artifact_type").AsString(30).NotNullable()
            .WithColumn("s3_key").AsString(500).NotNullable()
            .WithColumn("content_type").AsString(100).NotNullable()
            .WithColumn("size_bytes").AsInt64().Nullable()
            .WithColumn("retention_days").AsInt32().NotNullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("idx_artifact_doc").OnTable("pipe_document_artifacts")
            .OnColumn("document_id").Ascending();

        // 5. pipe_extraction_attempts (FK to pipe_documents)
        Create.Table("pipe_extraction_attempts")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_attempt_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("attempt_number").AsInt32().NotNullable()
            .WithColumn("phase").AsString(20).NotNullable()
            .WithColumn("model_id").AsString(100).NotNullable()
            .WithColumn("prompt_version").AsString(50).NotNullable()
            .WithColumn("status").AsString(30).NotNullable()
            .WithColumn("input_tokens").AsInt32().Nullable()
            .WithColumn("output_tokens").AsInt32().Nullable()
            .WithColumn("latency_ms").AsInt32().Nullable()
            .WithColumn("confidence").AsDecimal(5, 4).Nullable()
            .WithColumn("error_message").AsString(2000).Nullable()
            .WithColumn("response_artifact_id").AsInt64().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable()
            .WithColumn("completed_at").AsDateTime().Nullable();

        Create.Index("idx_attempt_doc").OnTable("pipe_extraction_attempts")
            .OnColumn("document_id").Ascending();

        // 6. pipe_canonical_outputs (FK to pipe_documents)
        Create.Table("pipe_canonical_outputs")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_canonical_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("schema_version").AsString(20).NotNullable()
            .WithColumn("output_json").AsCustom("LONGTEXT").NotNullable()
            .WithColumn("invoice_number").AsString(100).Nullable()
            .WithColumn("issue_date").AsDate().Nullable()
            .WithColumn("due_date").AsDate().Nullable()
            .WithColumn("supplier_cui").AsString(15).Nullable()
            .WithColumn("customer_cui").AsString(15).Nullable()
            .WithColumn("currency").AsString(3).Nullable()
            .WithColumn("total_amount").AsDecimal(15, 2).Nullable()
            .WithColumn("vat_amount").AsDecimal(15, 2).Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("uq_canonical_doc").OnTable("pipe_canonical_outputs")
            .OnColumn("document_id").Unique();

        // 7. pipe_document_events (FK to pipe_documents)
        Create.Table("pipe_document_events")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_event_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("event_type").AsString(50).NotNullable()
            .WithColumn("from_status").AsString(30).Nullable()
            .WithColumn("to_status").AsString(30).Nullable()
            .WithColumn("details").AsCustom("LONGTEXT").Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("idx_event_doc").OnTable("pipe_document_events")
            .OnColumn("document_id").Ascending();

        // 8. pipe_review_flags (FK to pipe_documents)
        Create.Table("pipe_review_flags")
            .WithColumn("id").AsInt64().PrimaryKey().Identity()
            .WithColumn("document_id").AsInt64().NotNullable()
                .ForeignKey("fk_flag_doc", "pipe_documents", "id")
            .WithColumn("tenant_id").AsInt64().NotNullable()
            .WithColumn("flag_type").AsString(50).NotNullable()
            .WithColumn("severity").AsString(20).NotNullable()
            .WithColumn("message").AsString(500).NotNullable()
            .WithColumn("is_resolved").AsBoolean().NotNullable()
            .WithColumn("resolved_at").AsDateTime().Nullable()
            .WithColumn("created_at").AsDateTime().NotNullable();

        Create.Index("idx_flag_doc").OnTable("pipe_review_flags")
            .OnColumn("document_id").Ascending();

        // Seed: dev-test-client
        Insert.IntoTable("cfg_api_clients").Row(new
        {
            name = "dev-test-client",
            api_key_hash = "d36ba41aff2b6153753a56e88f4c52cc4356ef300372b40b56dd95b0802b606c",
            api_key_prefix = "dp_dev_te",
            is_active = true,
            rate_limit_per_min = 100,
            max_file_size_mb = 50,
            created_at = SystemMethods.CurrentUTCDateTime,
            updated_at = SystemMethods.CurrentUTCDateTime
        });
    }

    public override void Down()
    {
        Delete.Table("pipe_review_flags");
        Delete.Table("pipe_document_events");
        Delete.Table("pipe_canonical_outputs");
        Delete.Table("pipe_extraction_attempts");
        Delete.Table("pipe_document_artifacts");
        Delete.Table("pipe_documents");
        Delete.Table("audit_job_executions");
        Delete.Table("cfg_api_clients");
    }
}
