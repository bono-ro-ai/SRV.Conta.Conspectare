# SRV.Conta.Conspectare

AI-powered document processing pipeline for Romanian accounting documents (invoices, receipts, credit notes).

## Stack

- **Backend**: .NET 9, NHibernate 5.5.2 + FluentNHibernate 3.4.0, MariaDB 11.4
- **LLM**: Anthropic Claude (claude-sonnet-4-20250514), Google Gemini (gemini-2.5-flash) -- multi-model consensus
- **Storage**: AWS S3 (document files, KMS-encrypted; LocalStack for dev)
- **Hosting**: Railway (GHCR images, deploy via GitHub Actions CI/CD)
- **Observability**: OpenTelemetry + Prometheus metrics

## Running Locally

```bash
docker compose up
```

This starts: `db` (MariaDB 11.4), `localstack` (S3), `migrate`, `api` (:5100), `worker` (:5101).

## Running Tests

```bash
# Primary (no local .NET SDK required)
docker run --rm -v "$(pwd):/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 dotnet test

# If you have the .NET 9 SDK installed locally
cd Conspectare.Tests && dotnet test
```

## DB Conventions

- snake_case column names
- Domain-prefixed tables: `pipe_`, `cfg_`, `audit_`, `sec_`
- FluentNHibernate `ClassMap<T>` with explicit `.Column("snake_case_name")`
- All mapped properties must be `virtual`

## CQRS Pattern

- `Load*Query` -- query by known key/identifier
- `Find*Query` -- query by search criteria
- `Save*Command` -- save/create command
- `[Verb]*Command` -- update/action command
- One class per file, file name matches class name

## Workers

The `Conspectare.Workers` project contains background service implementations. Workers run in a **separate container** (`Conspectare.WorkerHost`) from the API. Both containers share the same database and S3 storage via `SharedDependencyInjection`.

- **WorkerHost**: `Conspectare.WorkerHost/Program.cs` -- registers all 8 workers, exposes `/health` on port 5101.
- **Migrations**: Run via `dotnet Conspectare.Api.dll --migrate` (dedicated `migrate` service in docker-compose).

## Project Structure

| Folder | Description |
|--------|-------------|
| `Conspectare.Api/` | ASP.NET Core Web API -- controllers, DTOs, middleware, authentication |
| `Conspectare.Domain/` | Domain entities and enum constants (no dependencies) |
| `Conspectare.Infrastructure/` | NHibernate mappings, migrations (FluentMigrator), tenant filter |
| `Conspectare.Infrastructure.Llm/` | LLM API clients (Claude, Gemini) and DI registration |
| `Conspectare.Services/` | Business logic -- CQRS commands/queries, orchestration, processors, validation |
| `Conspectare.Workers/` | Background services -- triage, extraction, webhook, VAT retry, cleanup |
| `Conspectare.WorkerHost/` | Standalone host for running workers in a separate container |
| `Conspectare.Client/` | NuGet SDK package for consuming the Conspectare API |
| `Conspectare.Tests/` | xUnit test project (SQLite in-memory for NHibernate tests) |
| `docs/` | Project documentation |

## Docs Index

| Document | Purpose |
|----------|---------|
| `docs/ARCHITECTURE.md` | Architecture overview, project structure, tech stack, deployment |
| `docs/API_CONTRACTS.md` | All API endpoints with request/response shapes |
| `docs/DATA_MODEL.md` | Entity model, columns, indexes, multi-tenancy scope |
| `docs/INTEGRATIONS_AND_CRONS.md` | External integrations, background services, env vars |
| `docs/DEPLOYMENT.md` | Railway deployment setup and configuration |

## Documentation Maintenance

When a change affects any of the following, the corresponding documentation **must** be updated as part of the same commit or PR:

| Change Type | Documentation to Update |
|-------------|------------------------|
| New/removed API endpoint | `docs/API_CONTRACTS.md` |
| Architecture change (new service, pattern change) | `docs/ARCHITECTURE.md` |
| Data model change (new entity, relationship, column) | `docs/DATA_MODEL.md` |
| External integration added/changed | `docs/INTEGRATIONS_AND_CRONS.md` |
| Background service added/removed | `docs/INTEGRATIONS_AND_CRONS.md` and `docs/ARCHITECTURE.md` |
| New environment variable | `.env.example` and `docs/INTEGRATIONS_AND_CRONS.md` |
| Project structure change | `docs/ARCHITECTURE.md` |

**Rules**:
- Update the `Last updated` date in any modified doc file.
- Documentation is part of the definition of done -- not optional.

## Conventions

- **Commits**: `<gitmoji> type(<scope>): <summary>`
- **Branches**: `feat/<JIRA-KEY>-<slug>` or `fix/<JIRA-KEY>-<slug>`
- **Naming**: PascalCase for C# types, snake_case for DB columns and JSON fields

## Jira Project

GO

Last updated: 2026-03-28
