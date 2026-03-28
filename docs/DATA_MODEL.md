# Data Model

Last updated: 2026-03-28

## Overview

The database uses MariaDB 11.4 with FluentNHibernate ORM. All tables use snake_case column names with domain-prefixed table names (`pipe_`, `cfg_`, `audit_`, `sec_`). Multi-tenancy is enforced via NHibernate session-level filters on `tenant_id`.

---

## Entities

### Document (`pipe_documents`)

The central entity in the processing pipeline. Tracks a document from ingestion through triage, extraction, and completion.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `tenant_id` | BIGINT | No | Foreign key to `cfg_api_clients` |
| `external_ref` | VARCHAR | Yes | Caller-supplied idempotency/tracking reference |
| `content_hash` | VARCHAR | Yes | SHA-256 hash of raw file content |
| `file_name` | VARCHAR | No | Original file name |
| `content_type` | VARCHAR | Yes | MIME type |
| `file_size_bytes` | BIGINT | No | File size in bytes |
| `input_format` | VARCHAR | Yes | Detected format (see `InputFormat` enum) |
| `status` | VARCHAR | No | Processing status (see `DocumentStatus` enum) |
| `document_type` | VARCHAR | Yes | Classified type after triage (see `DocumentType` enum) |
| `triage_confidence` | DECIMAL | Yes | LLM confidence score (0-1) from triage |
| `is_accounting_relevant` | BOOLEAN | Yes | Whether the document is accounting-relevant |
| `retry_count` | INT | No | Number of processing retries |
| `max_retries` | INT | No | Max allowed retries (default 3) |
| `error_message` | TEXT | Yes | Last error message |
| `raw_file_s3_key` | VARCHAR | No | S3 key for the original uploaded file |
| `document_ref` | VARCHAR | Yes | Human-readable reference (e.g., `RO12345/2026/00001`) |
| `fiscal_code` | VARCHAR | Yes | CUI/fiscal code associated with the document |
| `client_reference` | VARCHAR | Yes | Client-supplied reference string |
| `metadata` | TEXT | Yes | Arbitrary metadata JSON |
| `uploaded_by` | VARCHAR | Yes | Identity of the uploader (email or api:name) |
| `created_at` | DATETIME | No | Ingestion timestamp |
| `updated_at` | DATETIME | No | Last modification timestamp |
| `completed_at` | DATETIME | Yes | Terminal status timestamp |

**Relationships**:
- Has many `DocumentArtifact` (artifacts)
- Has many `ExtractionAttempt` (extraction attempts)
- Has many `ReviewFlag` (review flags)
- Has one `CanonicalOutput` (canonical output)
- Has many `DocumentEvent` (events)
- Belongs to `ApiClient` (tenant)

---

### ApiClient (`cfg_api_clients`)

Represents a tenant (API consumer) with authentication credentials and configuration.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key / Tenant ID |
| `name` | VARCHAR(200) | No | Display name |
| `api_key_hash` | VARCHAR | No | SHA-256 hash of the API key |
| `api_key_prefix` | VARCHAR(8) | No | First 8 chars of API key (for lookup) |
| `is_active` | BOOLEAN | No | Whether the client can authenticate |
| `is_admin` | BOOLEAN | No | Admin privilege flag |
| `rate_limit_per_min` | INT | No | Request rate limit per minute |
| `max_file_size_mb` | INT | No | Maximum upload file size in MB |
| `webhook_url` | VARCHAR | Yes | Outbound webhook endpoint URL |
| `webhook_secret` | VARCHAR | Yes | HMAC secret for webhook signatures |
| `company_name` | VARCHAR | Yes | Company display name |
| `cui` | VARCHAR | Yes | Romanian fiscal code (CUI) |
| `contact_email` | VARCHAR | Yes | Contact email address |
| `trial_expires_at` | DATETIME | Yes | Trial period expiration |
| `created_at` | DATETIME | No | Creation timestamp |
| `updated_at` | DATETIME | No | Last modification timestamp |

---

### CanonicalOutput (`pipe_canonical_outputs`)

Stores the structured extraction result for a document, with denormalized invoice header fields for querying.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `schema_version` | VARCHAR | Yes | Output schema version (e.g., "1.0.0") |
| `output_json` | LONGTEXT | Yes | Legacy inline JSON (pre-migration) |
| `output_json_s3_key` | VARCHAR | Yes | S3 key for canonical output JSON |
| `invoice_number` | VARCHAR | Yes | Denormalized: extracted invoice number |
| `issue_date` | DATE | Yes | Denormalized: invoice issue date |
| `due_date` | DATE | Yes | Denormalized: invoice due date |
| `supplier_cui` | VARCHAR | Yes | Denormalized: supplier fiscal code |
| `customer_cui` | VARCHAR | Yes | Denormalized: customer fiscal code |
| `currency` | VARCHAR | Yes | Denormalized: currency code |
| `total_amount` | DECIMAL | Yes | Denormalized: total amount |
| `vat_amount` | DECIMAL | Yes | Denormalized: VAT amount |
| `consensus_strategy` | VARCHAR | Yes | Strategy used (e.g., "highest_confidence") |
| `winning_model_id` | VARCHAR | Yes | Model that produced the winning result |
| `created_at` | DATETIME | No | Creation timestamp |

**Relationships**: Belongs to `Document`

---

### ExtractionAttempt (`pipe_extraction_attempts`)

Records each LLM extraction or triage call for auditability and cost tracking.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `attempt_number` | INT | No | Sequential attempt number |
| `phase` | VARCHAR | No | Pipeline phase (see `PipelinePhase` enum) |
| `model_id` | VARCHAR | Yes | LLM model identifier used |
| `prompt_version` | VARCHAR | Yes | Prompt version used |
| `status` | VARCHAR | No | Attempt status (see `ExtractionAttemptStatus` enum) |
| `input_tokens` | INT | Yes | Tokens consumed (input) |
| `output_tokens` | INT | Yes | Tokens consumed (output) |
| `latency_ms` | INT | Yes | LLM call latency in milliseconds |
| `confidence` | DECIMAL | Yes | Model-reported confidence score |
| `error_message` | TEXT | Yes | Error message if failed |
| `response_artifact_id` | BIGINT | Yes | FK to `pipe_document_artifacts` for raw LLM response |
| `provider_key` | VARCHAR | Yes | Provider identifier (e.g., "claude", "gemini") |
| `created_at` | DATETIME | No | Attempt start timestamp |
| `completed_at` | DATETIME | Yes | Attempt completion timestamp |

**Relationships**: Belongs to `Document`

---

### DocumentArtifact (`pipe_document_artifacts`)

Stores references to files generated during processing (raw uploads, OCR text, LLM responses).

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `artifact_type` | VARCHAR | No | Type (see `ArtifactType` enum) |
| `s3_key` | VARCHAR | No | S3 storage key |
| `content_type` | VARCHAR | Yes | MIME type |
| `size_bytes` | BIGINT | Yes | File size |
| `retention_days` | INT | No | Retention period in days |
| `created_at` | DATETIME | No | Creation timestamp |

**Relationships**: Belongs to `Document`

---

### ReviewFlag (`pipe_review_flags`)

Flags raised by the LLM or validation logic that require human review.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `flag_type` | VARCHAR | No | Flag category (e.g., "vat_invalid", "low_confidence") |
| `severity` | VARCHAR | No | Severity level (see `ReviewFlagSeverity` enum) |
| `message` | TEXT | No | Human-readable description |
| `is_resolved` | BOOLEAN | No | Whether the flag has been addressed |
| `resolved_at` | DATETIME | Yes | Resolution timestamp |
| `created_at` | DATETIME | No | Creation timestamp |

**Relationships**: Belongs to `Document`

---

### DocumentEvent (`pipe_document_events`)

Immutable audit log of all state transitions and significant actions on a document.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `event_type` | VARCHAR | No | Event type (see `DocumentEventType` enum) |
| `from_status` | VARCHAR | Yes | Previous status |
| `to_status` | VARCHAR | Yes | New status |
| `details` | TEXT | Yes | Additional event details (JSON) |
| `created_at` | DATETIME | No | Event timestamp |

**Relationships**: Belongs to `Document`

---

### WebhookDelivery (`pipe_webhook_deliveries`)

Tracks outbound webhook delivery attempts with retry logic.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `document_id` | BIGINT | No | FK to `pipe_documents` |
| `tenant_id` | BIGINT | No | Tenant scope |
| `webhook_url` | VARCHAR | No | Target URL |
| `payload_json` | LONGTEXT | No | JSON payload sent |
| `status` | VARCHAR | No | Delivery status (see `WebhookDeliveryStatus` enum) |
| `http_status_code` | INT | No | Last HTTP response code |
| `error_message` | TEXT | Yes | Last error message |
| `attempt_count` | INT | No | Number of delivery attempts |
| `max_attempts` | INT | No | Maximum delivery attempts |
| `next_attempt_at` | DATETIME | Yes | Scheduled next retry |
| `last_attempt_at` | DATETIME | Yes | Last attempt timestamp |
| `delivered_at` | DATETIME | Yes | Successful delivery timestamp |
| `created_at` | DATETIME | No | Creation timestamp |
| `updated_at` | DATETIME | No | Last modification timestamp |

---

### User (`sec_users`)

Dashboard users with email/password or Google OAuth authentication.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `email` | VARCHAR | No | Email address (unique) |
| `name` | VARCHAR | Yes | Display name |
| `password_hash` | VARCHAR | Yes | BCrypt password hash |
| `role` | VARCHAR | No | Role ("user" or "admin") |
| `is_active` | BOOLEAN | No | Account active flag |
| `failed_login_attempts` | INT | No | Failed login counter |
| `tenant_id` | BIGINT | Yes | FK to `cfg_api_clients` |
| `locked_until` | DATETIME | Yes | Account lockout expiration |
| `last_login_at` | DATETIME | Yes | Last successful login |
| `google_id` | VARCHAR | Yes | Google OAuth subject ID |
| `avatar_url` | VARCHAR | Yes | Google profile avatar URL |
| `created_at` | DATETIME | No | Creation timestamp |
| `updated_at` | DATETIME | No | Last modification timestamp |

---

### RefreshToken (`sec_refresh_tokens`)

Tracks JWT refresh tokens with rotation support.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `user_id` | BIGINT | No | FK to `sec_users` |
| `token_hash` | VARCHAR | No | SHA-256 hash of the raw token |
| `expires_at` | DATETIME | No | Token expiration |
| `created_at` | DATETIME | No | Creation timestamp |
| `revoked_at` | DATETIME | Yes | Revocation timestamp |
| `replaced_by_token_id` | BIGINT | Yes | FK to replacement token (rotation chain) |

**Relationships**: Belongs to `User`

---

### MagicLinkToken (`sec_magic_link_tokens`)

One-time passwordless sign-in tokens sent via email.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `user_id` | BIGINT | Yes | FK to `sec_users` (null for unknown emails) |
| `token_hash` | VARCHAR | No | SHA-256 hash of the magic link token |
| `email` | VARCHAR | No | Target email address |
| `expires_at` | DATETIME | No | Token expiration |
| `used_at` | DATETIME | Yes | Redemption timestamp |
| `created_at` | DATETIME | No | Creation timestamp |
| `ip_address` | VARCHAR | Yes | Requester IP address |

**Relationships**: Belongs to `User` (optional)

---

### PromptVersion (`cfg_prompt_versions`)

Versioned LLM prompt templates with A/B traffic weighting.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `phase` | VARCHAR | No | Pipeline phase ("triage" or "extraction") |
| `document_type` | VARCHAR | Yes | Document type scope (null = all types) |
| `version` | VARCHAR | No | Semantic version string |
| `prompt_text` | LONGTEXT | No | Full prompt template text |
| `is_active` | BOOLEAN | No | Whether this version is in use |
| `traffic_weight` | INT | No | A/B test traffic weight |
| `created_at` | DATETIME | No | Creation timestamp |
| `updated_at` | DATETIME | No | Last modification timestamp |

---

### UsageDaily (`cfg_usage_daily`)

Pre-aggregated daily usage metrics per tenant for reporting.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `tenant_id` | BIGINT | No | FK to `cfg_api_clients` |
| `usage_date` | DATE | No | Aggregation date |
| `documents_ingested` | INT | No | Documents ingested that day |
| `documents_processed` | INT | No | Documents completed that day |
| `llm_input_tokens` | BIGINT | No | Total LLM input tokens consumed |
| `llm_output_tokens` | BIGINT | No | Total LLM output tokens consumed |
| `llm_requests` | INT | No | Total LLM API calls |
| `storage_bytes` | BIGINT | No | Storage consumed in bytes |
| `api_calls` | INT | No | Total API calls |
| `created_at` | DATETIME | No | Row creation timestamp |
| `updated_at` | DATETIME | No | Last upsert timestamp |

---

### JobExecution (`audit_job_executions`)

Audit log for background worker runs.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `job_name` | VARCHAR | No | Worker name (e.g., "triage_worker") |
| `instance_id` | VARCHAR | No | Host + random suffix |
| `started_at` | DATETIME | No | Execution start |
| `completed_at` | DATETIME | Yes | Execution end |
| `duration_ms` | INT | Yes | Execution duration in milliseconds |
| `status` | VARCHAR | No | Status (see `JobExecutionStatus` enum) |
| `items_processed` | INT | Yes | Number of items processed |
| `error_message` | TEXT(2000) | Yes | Error message (truncated to 2000 chars) |
| `created_at` | DATETIME | No | Row creation timestamp |

---

### DocumentRefSequence (`pipe_document_ref_sequences`)

Per-fiscal-code, per-year sequence counter for generating human-readable document references.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | BIGINT | PK, auto | Primary key |
| `fiscal_code` | VARCHAR | No | CUI/fiscal code |
| `year` | INT | No | Calendar year |
| `last_seq` | INT | No | Last allocated sequence number |

**Unique constraint**: (`fiscal_code`, `year`)

---

## Enums

### DocumentStatus
`ingested`, `pending_triage`, `triaging`, `pending_extraction`, `extracting`, `completed`, `extraction_failed`, `review_required`, `rejected`, `failed`

### ExternalDocumentStatus
`processing`, `completed`, `failed`, `review_required`, `rejected`

### DocumentType
`invoice`, `credit_note`, `receipt`, `proforma`, `non_accounting`, `unknown`

### InputFormat
`xml_efactura`, `pdf`, `image`, `json`, `csv`, `unknown`

### PipelinePhase
`triage`, `extraction`

### ArtifactType
`raw`, `ocr_text`, `llm_triage_response`, `llm_extraction_response`, `canonical_json`

### ExtractionAttemptStatus
`completed`, `failed`, `completed_non_winner`

### ReviewFlagSeverity
`info`, `warning`, `error`

### WebhookDeliveryStatus
`pending`, `delivered`, `failed_permanently`

### JobExecutionStatus
`completed`, `failed`, `cancelled`

### DocumentEventType
`ingested`, `status_change`, `resolved`, `canonical_output_edited`, `vat_validation_completed`
