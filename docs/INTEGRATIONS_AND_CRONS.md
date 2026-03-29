# Integrations and Background Services

Last updated: 2026-03-28

---

## I. AI/ML Services

### Anthropic Claude API

- **Purpose**: Document triage (classification) and structured data extraction
- **Model**: `claude-sonnet-4-20250514` (configurable via `Claude:Model`)
- **Integration**: `ClaudeApiClient` in `Conspectare.Infrastructure.Llm/Claude/`
- **API**: Claude Messages API (`/v1/messages`) with tool-use for structured output
- **Features**:
  - Forced tool calling (`classify_document` for triage, `extract_invoice_data` for extraction)
  - Base64-encoded PDF and image support
  - Exponential back-off retry on 429/503 (5s, 15s, 30s)
  - Max 3 retries by default
- **Config section**: `Claude`

### Google Gemini API

- **Purpose**: Alternative/secondary LLM for multi-model consensus extraction
- **Model**: `gemini-2.5-flash` (configurable via `Gemini:Model`)
- **Integration**: `GeminiApiClient` in `Conspectare.Infrastructure.Llm/Gemini/`
- **API**: Gemini Generative Language API (`generateContent`)
- **Features**: Function calling for structured output, same retry logic as Claude
- **Config section**: `Gemini`

### Multi-Model Consensus

- **Feature flag**: `Llm:MultiModel:Enabled`
- **Strategy**: `HighestConfidenceStrategy` -- selects extraction result with highest confidence score
- **Implementation**: `MultiModelExtractionService` runs both Claude and Gemini, `LlmClientFactory` provides keyed client resolution

---

## II. Document Storage (AWS S3)

- **Purpose**: Stores raw uploaded files, processing artifacts, and canonical output JSON
- **Implementation**: `S3StorageService` in `Conspectare.Services/Infrastructure/`
- **Key taxonomy** (via `S3KeyBuilder`):
  - Input files: `tenants/{tenantId}/input/{guid}/{fileName}`
  - Artifacts: `tenants/{tenantId}/artifacts/{documentId}/{artifactFileName}`
  - Output: `tenants/{tenantId}/output/{documentId}/{outputFileName}`
- **Dev**: LocalStack (port 4566, S3 service only)
- **Prod**: AWS S3 with KMS encryption
- **Config section**: `Aws`

---

## III. External APIs

### ANAF VAT Validation (Deprecated -- replaced by local CUI validation)

- **Purpose**: Was used to validate Romanian fiscal codes (CUI) against the ANAF registry
- **Replacement**: `CuiValidator` provides local algorithmic validation (check digit verification)
- **Legacy client**: `AnafVatValidationClient` still present but unused in pipeline
- **Config section**: `Anaf`

### P&L Expense Tracker Integration

- **Purpose**: Upstream consumer of processed documents
- **Integration**: Webhook-based -- Conspectare sends webhook payloads when documents reach terminal states
- **Seed data**: `Migration_008_SeedPnlApiClient` creates a pre-configured API client for the P&L system
- **Config**: `PNL_API_URL`, `PNL_WEBHOOK_SECRET` (dev seed only)

---

## IV. Email Service

### Mandrill (Mailchimp Transactional)

- **Purpose**: Sends magic-link authentication emails
- **Implementation**: `MandrillEmailService` in `Conspectare.Services/Email/`
- **Features**: Branded HTML templates matching Bono design system
- **Config section**: `Mandrill`

---

## V. Authentication Providers

### Google OAuth 2.0

- **Purpose**: SSO login for dashboard users
- **Flow**: Server-side authorization code flow with HMAC-signed state token
- **Restriction**: Login restricted to `@bono.ro` domain (Google Workspace group check)
- **Implementation**: `GoogleTokenValidator`, `GoogleGroupChecker` in `Conspectare.Services/Auth/`
- **Config section**: `Google`

---

## VI. Background Services (Workers)

All workers run in the `Conspectare.WorkerHost` container (port 5101). They extend `DistributedBackgroundService` which provides distributed locking via MariaDB `GET_LOCK()`, adaptive back-off, and audit logging.

| Worker | Interval | Distributed Lock | Description |
|--------|----------|-------------------|-------------|
| `TriageWorker` | 3s (signal-driven) | `triage_worker` | Claims batch of 5 pending documents, classifies via LLM |
| `ExtractionWorker` | 3s (signal-driven) | `extraction_worker` | Claims batch of 5 triaged documents, extracts structured data via LLM |
| `WebhookWorker` | 5s | `webhook_worker` | Dispatches batch of 10 pending webhook deliveries |
| `VatRetryWorker` | 5min | `vat_retry_worker` | Retries batch of 10 failed CUI validation flags |
| `StaleClaimRecoveryWorker` | 2min | `stale_claim_recovery` | Recovers documents stuck in claimed state >5 minutes |
| `UsageAggregationWorker` | 1h | `usage_aggregation` | Aggregates previous day's usage for all active tenants |
| `AuditCleanupWorker` | 6h | `audit_cleanup_worker` | Deletes audit rows older than 30 days (batch of 10,000) |
| `MemoryRecyclingWorker` | 60s | None (per-instance) | Triggers GC when idle and heap exceeds 100 MB (5min cooldown) |

---

## VII. Observability

### Prometheus Metrics

- **Endpoint**: `GET /metrics` on both API and Worker
- **Meter name**: `Conspectare`
- **Key metrics**:
  - `conspectare.documents.ingested` -- Counter by input format
  - `conspectare.documents.completed` -- Counter
  - `conspectare.documents.failed` -- Counter by phase and error type
  - `conspectare.processing.duration` -- Histogram by pipeline phase (ms)
  - `conspectare.llm.call.duration` -- Histogram by provider and phase (ms)
  - `conspectare.llm.tokens` -- Counter by provider and direction (input/output)
  - Memory recycling triggered counter

### Correlation IDs

`CorrelationIdMiddleware` generates or forwards `X-Correlation-Id` header on all API requests.

### Structured Logging

JSON console logger for Railway log aggregation.

---

## VIII. Environment Variables

### Required

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__ConspectareDb` | MariaDB connection string |
| `Aws__AccessKeyId` | AWS access key |
| `Aws__SecretAccessKey` | AWS secret key |
| `Aws__Region` | AWS region (e.g., `eu-central-1`) |
| `Aws__BucketName` | S3 bucket name |
| `Jwt__Secret` | JWT signing key (min 32 chars) |
| `Jwt__Issuer` | JWT issuer (default: `conspectare-api`) |
| `Jwt__Audience` | JWT audience (default: `conspectare-dashboard`) |

### LLM Configuration

| Variable | Description |
|----------|-------------|
| `Llm__Provider` | Active LLM provider: `claude` (default) or `gemini` |
| `Llm__MultiModel__Enabled` | Enable multi-model consensus (default: `false`) |
| `Claude__ApiKey` | Anthropic API key |
| `Claude__Model` | Claude model ID (default: `claude-sonnet-4-20250514`) |
| `Claude__MaxTokens` | Max output tokens (default: `4096`) |
| `Claude__BaseUrl` | API base URL (default: `https://api.anthropic.com`) |
| `Claude__TimeoutSeconds` | Request timeout (default: `60`) |
| `Claude__MaxRetries` | Retry count (default: `3`) |
| `Gemini__ApiKey` | Google API key |
| `Gemini__Model` | Gemini model ID (default: `gemini-2.5-flash`) |
| `Gemini__TriageModel` | Optional triage-specific model override |
| `Gemini__MaxOutputTokens` | Max output tokens (default: `4096`) |
| `Gemini__BaseUrl` | API base URL (default: `https://generativelanguage.googleapis.com`) |

### Optional

| Variable | Description |
|----------|-------------|
| `Aws__ServiceUrl` | S3-compatible endpoint (e.g., `http://localstack:4566` for dev) |
| `Google__ClientId` | Google OAuth client ID |
| `Google__ClientSecret` | Google OAuth client secret |
| `Mandrill__ApiKey` | Mandrill transactional email API key |
| `Mandrill__DefaultSender` | Sender email address |
| `Mandrill__DefaultSenderName` | Sender display name |
| `App__FrontendUrl` | Frontend URL for OAuth redirects and magic links |
| `Anaf__BaseUrl` | ANAF VAT API base URL (legacy) |
| `Anaf__TimeoutSeconds` | ANAF API timeout (default: `10`) |
| `Anaf__MaxRetries` | ANAF retry count (default: `2`) |
| `Cors__AllowedOrigins` | Comma-separated CORS origins (empty = allow all) |
| `PORT` | API listen port (default: `5100`) |

---

## IX. Webhook Delivery

When a document reaches a terminal state (`completed`, `failed`, `rejected`, `review_required`), a webhook delivery is created and dispatched by the `WebhookWorker`.

- **Signing**: Payloads are signed with the tenant's `webhook_secret` via HMAC-SHA256
- **Retries**: Up to `max_attempts` with exponential back-off
- **Permanent failure**: After exhausting retries or if the API client is deleted
- **Payload builder**: `WebhookPayloadBuilder` constructs the JSON payload
- **Implementation**: `WebhookDispatchService`, `WebhookEnqueuer`, `WebhookNotifier`
