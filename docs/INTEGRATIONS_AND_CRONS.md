# Integrations & Crons

Last updated: 2026-03-23

## I. P&L Expense Tracker Integration

Conspectare notifies the P&L Expense Tracker service when document processing completes via a webhook callback.

### Webhook

- **URL pattern**: `{PNL_API_URL}/api/v1/webhooks/conspectare`
- **Method**: POST
- **Authentication**: HMAC-SHA256 signature using the `webhook_secret` stored on the ApiClient record
- **Trigger**: Document reaches a terminal status (Completed, Failed)

### API Client

- **Name**: `P&L Expense Tracker`
- **API key prefix**: `csp_pnl_`
- **Rate limit**: 60 req/min
- **Max file size**: 10 MB
- **Admin**: No

## II. Deployment Topology

Conspectare runs as two separate containers sharing the same database and S3 storage:

| Container | Project | Port | Purpose |
|-----------|---------|------|---------|
| `api` | `Conspectare.Api` | 5100 | HTTP API (controllers, authentication, middleware) |
| `worker` | `Conspectare.WorkerHost` | 5101 | Background workers (triage, extraction, webhooks, etc.) |

Both containers register shared infrastructure (NHibernate, S3, metrics) via `SharedDependencyInjection` in `Conspectare.Services` and LLM clients via `LlmDependencyInjection` in `Conspectare.Infrastructure.Llm`.

### Health Checks

- **API**: `GET /health` on port 5100
- **Worker**: `GET /health` on port 5101

### Observability

Both containers expose a Prometheus metrics scraping endpoint at `/metrics` via `MapPrometheusScrapingEndpoint()`.

## III. Background Workers

Workers run as `IHostedService` inside the **worker container** (`Conspectare.WorkerHost`).

| Worker | Job Name | Interval | Description |
|--------|----------|----------|-------------|
| `TriageWorker` | `triage` | Signal-driven | Classifies incoming documents |
| `ExtractionWorker` | `extraction` | Signal-driven | Extracts data from documents via LLM |
| `WebhookWorker` | `webhook_dispatch` | Signal-driven | Dispatches webhook callbacks to API clients |
| `VatRetryWorker` | `vat_retry` | 5 min | Retries failed VAT validation calls |
| `StaleClaimRecoveryWorker` | `stale_claim_recovery` | 2 min | Recovers stuck documents |
| `UsageAggregationWorker` | `usage_aggregation` | 1 hour | Aggregates daily per-tenant usage metrics into `audit_usage_daily`. Cross-tenant, idempotent (UPSERT on tenant_id + usage_date). |
| `AuditCleanupWorker` | `audit_cleanup` | Periodic | Cleans up old audit records |

## IV. Cron Jobs

None currently configured.

## V. Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `ConnectionStrings__ConspectareDb` | Yes | — | MariaDB connection string |
| `Aws__ServiceUrl` | No | — | S3-compatible endpoint (LocalStack for dev) |
| `Aws__AccessKeyId` | Yes | — | AWS access key |
| `Aws__SecretAccessKey` | Yes | — | AWS secret key |
| `Aws__Region` | No | `eu-central-1` | AWS region |
| `Aws__BucketName` | Yes | — | S3 bucket for document storage |
| `Claude__ApiKey` | No | — | Anthropic API key (required when Llm__Provider=claude) |
| `Gemini__ApiKey` | No | — | Google Gemini API key (required when Llm__Provider=gemini) |
| `Llm__Provider` | No | `claude` | Active LLM provider (`claude` or `gemini`) |
| `Llm__MultiModel__Enabled` | No | `false` | Enable multi-model consensus extraction |
| `PNL_API_URL` | No | `http://localhost:5200` | Base URL for the P&L Expense Tracker service. Used to construct the webhook callback URL during ApiClient seed migration. |
