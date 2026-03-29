# Architecture

Last updated: 2026-03-28

## 1. System Overview

**SRV.Conta.Conspectare** is an AI-powered document processing pipeline for Romanian accounting documents. It ingests documents (PDFs, images, e-Factura XML, CSV, JSON), classifies them using LLM-based triage, extracts structured accounting data via multi-model consensus, validates fiscal codes (CUI), and delivers results through webhooks and a REST API.

The system is designed as a multi-tenant SaaS platform where each API client (tenant) has isolated data via NHibernate session-level filters.

## 2. Tech Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Runtime | .NET | 9.0 |
| ORM | NHibernate + FluentNHibernate | 5.5.2 / 3.4.0 |
| Database | MariaDB | 11.4 |
| Migrations | FluentMigrator | (via NuGet) |
| Object Storage | AWS S3 (LocalStack for dev) | -- |
| LLM (primary) | Anthropic Claude | claude-sonnet-4-20250514 |
| LLM (secondary) | Google Gemini | gemini-2.5-flash |
| Metrics | OpenTelemetry + Prometheus | 1.11.2 / 1.9.0 |
| API Docs | OpenAPI + Scalar | 2.4.1 |
| Auth | JWT Bearer + API Key (dual scheme) | ASP.NET 9 |
| Container Runtime | Docker (Alpine-based images) | -- |
| CI/CD | GitHub Actions | -- |
| Hosting | Railway | -- |
| Container Registry | GitHub Container Registry (GHCR) | -- |

## 3. Project Structure

```
SRV.Conta.Conspectare/
|-- Conspectare.Api/              # ASP.NET Core Web API host
|   |-- Authentication/           # API key handler, scheme constants
|   |-- Configuration/            # DI registration, Startup pipeline
|   |-- Controllers/              # REST controllers (7 controllers)
|   |-- DTOs/                     # Request/response record types
|   |-- Extensions/               # OperationResult -> IActionResult, cookie helpers
|   |-- Middleware/                # CorrelationId, GlobalException, RateLimiting, Tenant
|   |-- Program.cs                # Entry point (migration mode or API mode)
|   +-- Dockerfile                # Multi-stage Alpine build
|
|-- Conspectare.Domain/           # Pure domain layer (no dependencies)
|   |-- Entities/                 # 14 entity classes
|   +-- Enums/                    # 11 enum/constant classes
|
|-- Conspectare.Infrastructure/   # Persistence infrastructure
|   |-- Mappings/                 # FluentNHibernate ClassMap<T> (15 maps)
|   |-- Migrations/               # FluentMigrator migrations (001-018)
|   |-- NHibernate/               # Session helpers, generic commands/queries
|   |-- Filters/                  # TenantFilterDefinition
|   +-- Settings/                 # AwsSettings
|
|-- Conspectare.Infrastructure.Llm/  # LLM integration layer
|   |-- Claude/                   # ClaudeApiClient, ClaudeApiSettings
|   |-- Gemini/                   # GeminiApiClient, GeminiApiSettings
|   +-- Configuration/            # LlmDependencyInjection
|
|-- Conspectare.Services/         # Business logic layer
|   |-- Auth/                     # Google OAuth, JWT token helpers
|   |-- Commands/                 # CQRS write operations (25+ commands)
|   |-- Configuration/            # Shared DI, settings classes, validation
|   |-- Core/Database/            # NHibernate base classes
|   |-- Email/                    # MandrillEmailService
|   |-- Extraction/               # Orchestration, multi-model, consensus
|   |-- ExternalIntegrations/     # ANAF VAT validation
|   |-- Infrastructure/           # S3StorageService, S3KeyBuilder, distributed lock
|   |-- Interfaces/               # Service contracts (20+ interfaces)
|   |-- Models/                   # Service-layer DTOs
|   |-- Observability/            # Prometheus metrics
|   |-- Processors/               # Document format processors (PDF, Image, XML)
|   |-- Prompts/                  # Versioned LLM prompt templates
|   |-- Queries/                  # CQRS read operations (25+ queries)
|   |-- Triage/                   # TriageOrchestrationService
|   +-- Validation/               # CUI validator
|
|-- Conspectare.Workers/          # Background worker implementations
|   |-- DistributedBackgroundService.cs  # Abstract base with locking + metrics
|   |-- TriageWorker.cs           # Document classification
|   |-- ExtractionWorker.cs       # Data extraction
|   |-- WebhookWorker.cs          # Outbound webhook delivery
|   |-- VatRetryWorker.cs         # Failed VAT validation retry
|   |-- UsageAggregationWorker.cs # Daily usage rollup
|   |-- AuditCleanupWorker.cs     # Old audit row pruning
|   |-- StaleClaimRecoveryWorker.cs  # Stuck document recovery
|   +-- MemoryRecyclingWorker.cs  # Idle GC compaction
|
|-- Conspectare.WorkerHost/       # Standalone worker container host
|   |-- Program.cs                # Registers all 8 workers, health check on :5101
|   +-- Dockerfile                # Separate Docker image
|
|-- Conspectare.Client/           # NuGet SDK package for API consumers
|   |-- ConspectareClient.cs      # Typed HTTP client
|   |-- IConspectareClient.cs     # Client interface
|   +-- Models/                   # Client-side DTOs
|
|-- Conspectare.Tests/            # xUnit tests (SQLite in-memory)
|-- docs/                         # Documentation
|-- .github/workflows/            # CI (build+test+Docker) and NuGet publish
+-- docker-compose.yml            # Local dev environment
```

## 4. Key Architectural Patterns

### 4.1 Document Processing Pipeline

Documents flow through a two-stage pipeline:

```
Ingest -> Triage (classification) -> Extraction (data capture) -> Review/Complete
```

1. **Ingestion**: API receives file upload, stores raw file in S3, creates `Document` record in `ingested` status, signals pipeline.
2. **Triage**: `TriageWorker` claims pending documents, sends to LLM for classification (document type, accounting relevance, confidence). Updates document status to `pending_extraction` or `rejected`.
3. **Extraction**: `ExtractionWorker` claims triage-complete documents, sends to LLM(s) for structured data extraction. Supports single-model and multi-model consensus modes. Creates `CanonicalOutput` with denormalized invoice fields. Runs CUI validation. Flags for review or marks complete.
4. **Post-processing**: VAT retry for failed validations, webhook delivery to notify consumers.

### 4.2 Multi-Model Consensus

When enabled (`Llm:MultiModel:Enabled=true`), the extraction stage runs the document through multiple LLM providers (Claude + Gemini) in parallel. A `HighestConfidenceStrategy` selects the winning extraction result based on reported confidence scores.

### 4.3 CQRS (Command Query Responsibility Segregation)

All database operations follow CQRS:
- **Queries**: Read-only, return data. Named `Load*Query` (by ID) or `Find*Query` (by criteria).
- **Commands**: Write operations. Named `Save*Command` (create) or `[Verb]*Command` (update/action).
- Each is a single class in its own file executing via NHibernate session.

### 4.4 Multi-Tenancy

- Each API client (tenant) has a unique `TenantId`.
- NHibernate session-level filter (`TenantFilterDefinition`) auto-scopes all queries.
- `TenantMiddleware` activates the filter after authentication.
- Cross-tenant operations (workers) explicitly disable the filter or operate without it.

### 4.5 Dual Authentication

The API supports two auth schemes via a policy-based router:
- **API Key**: Opaque bearer token (no dots). Looked up by 8-char prefix, verified via SHA-256 hash with constant-time comparison.
- **JWT Bearer**: Standard JWT tokens with `tenantId`, `role`, and `email` claims. Used by the dashboard UI.

The `DualAuth` policy scheme inspects the token shape and forwards to the appropriate handler.

### 4.6 Distributed Background Services

All workers extend `DistributedBackgroundService`, which provides:
- **Distributed locking** via MariaDB `GET_LOCK()` to prevent concurrent execution across replicas.
- **Adaptive back-off**: Doubles interval on idle runs, resets when work is found.
- **Pipeline signal**: Event-driven wake-up (via `PipelineSignal`) so triage/extraction workers react immediately to new documents.
- **Audit logging**: Every execution is recorded in `audit_job_executions` with duration, items processed, and error messages.

## 5. Authentication and Authorization

| Method | Scheme | Use Case |
|--------|--------|----------|
| API Key | `Bearer <opaque-key>` | Machine-to-machine (P&L integration) |
| JWT (email/password) | `Bearer <jwt>` | Dashboard login |
| Google OAuth | Authorization code flow | Dashboard SSO (restricted to `@bono.ro`) |
| Magic Link | Email-based OTP | Passwordless dashboard login |
| Refresh Token | HttpOnly cookie | Token rotation |

Admin endpoints require `IsAdmin` flag on the tenant context. JWT users can impersonate tenants via `X-Tenant-Id` header.

## 6. Background Services

| Worker | Interval | Purpose |
|--------|----------|---------|
| `TriageWorker` | 3s (signal-driven) | Classifies documents via LLM |
| `ExtractionWorker` | 3s (signal-driven) | Extracts structured data via LLM |
| `WebhookWorker` | 5s | Dispatches outbound webhook deliveries |
| `VatRetryWorker` | 5min | Retries failed CUI validations |
| `StaleClaimRecoveryWorker` | 2min | Recovers stuck documents (>5min stale threshold) |
| `UsageAggregationWorker` | 1h | Aggregates daily usage per tenant |
| `AuditCleanupWorker` | 6h | Deletes audit rows older than 30 days |
| `MemoryRecyclingWorker` | 60s | Triggers GC when idle and heap > 100 MB |

## 7. Deployment

### Container Architecture

Two separate containers deployed on Railway:
- **API** (`Conspectare.Api/Dockerfile`): ASP.NET Core web server on port 5100.
- **Worker** (`Conspectare.WorkerHost/Dockerfile`): Background service host on port 5101 (health check only).

Both are Alpine-based, multi-stage Docker builds. A dedicated `migrate` container runs FluentMigrator on startup.

### CI/CD Pipeline

GitHub Actions workflow (`.github/workflows/ci.yml`):
1. **Build & Test**: Restore, build, run xUnit tests.
2. **Docker Build**: Verify both Dockerfiles build successfully.
3. **Push to GHCR**: On `main` push, builds and pushes tagged images to GitHub Container Registry.

### Railway Configuration

`railway.toml`: Builds from `Conspectare.Api/Dockerfile`, health check at `/health` with 60s timeout, `ON_FAILURE` restart policy with max 5 retries.

## 8. Observability

- **Prometheus metrics** exposed at `/metrics` (both API and Worker).
- Custom `ConspectareMetrics` meter tracks:
  - Documents ingested/completed/failed (by format, phase, error type)
  - Processing duration histograms (by pipeline phase)
  - LLM call duration and token usage (by provider)
  - Memory recycling events
- **Correlation IDs**: `CorrelationIdMiddleware` adds `X-Correlation-Id` to all requests/responses.
- **Structured logging**: JSON console logger for Railway log aggregation.

## 9. ADR Index

No formal ADR directory exists yet. Notable architectural decisions are documented implicitly:
- Multi-model consensus extraction (Migration_004)
- Canonical output JSON moved from DB LONGTEXT to S3 (Migration_018)
- Worker process separated from API (PR #68)
- Google OAuth restricted to `@bono.ro` domain (PR #70)
- CUI validation moved from ANAF API to local algorithm (PR #71)
