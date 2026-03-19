# SRV.Conta.Conspectare

Document pipeline microservice for conta-facturare.

## Stack

- **Backend**: .NET 9, NHibernate 5.5.2 + FluentNHibernate 3.4.0, MariaDB 11.4
- **Storage**: AWS S3 (document files, KMS-encrypted)
- **Hosting**: Railway (GHCR images, deploy via GitHub Actions CD pipeline)

## DB Conventions

- snake_case column names
- Domain-prefixed tables: `pipe_`, `cfg_`, `audit_`, `sec_`
- FluentNHibernate `ClassMap<T>` with explicit `.Column("snake_case_name")`
- All mapped properties must be `virtual`

## CQRS Pattern

- `Load*Query` — query by known key/identifier
- `Find*Query` — query by search criteria
- `Save*Command` — save/create command
- `[Verb]*Command` — update/action command
- `SaveOrUpdateCommand.For(entity)` — generic single-entity save
- One class per file, file name matches class name

## Workers

The `Conspectare.Workers` project contains background service implementations (e.g., TriageWorker, ExtractionWorker) that run as `IHostedService` registrations inside the API host process. In Phase 1 there is no separate container or docker-compose service for workers — they share the API process. A dedicated worker container may be introduced in a later phase if scaling demands it.

## Running Tests

```bash
# Primary (no local .NET SDK required)
docker run --rm -v "$(pwd):/src" -w /src mcr.microsoft.com/dotnet/sdk:9.0 dotnet test

# If you have the .NET 9 SDK installed locally
cd Conspectare.Tests && dotnet test
```

## Running Locally

```bash
docker compose up
```

## Commits

```
<gitmoji> type(<scope>): <summary>
```

## Branches

```
feat/<JIRA-KEY>-<slug>
fix/<JIRA-KEY>-<slug>
```

## Deployment

Railway (staging). See `docs/DEPLOYMENT.md` for setup, env vars, and health check details.

## Jira Project

GO
