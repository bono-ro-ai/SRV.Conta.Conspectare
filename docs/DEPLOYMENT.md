# Deployment Guide

Last updated: 2026-03-23

## Platform

SRV.Conta.Conspectare is deployed on **Railway** using GHCR (GitHub Container Registry) images built by the GitHub Actions CD pipeline.

## Railway Project Setup

These steps are performed once, manually, via the Railway dashboard:

1. **Create a new Railway project** (e.g. `conspectare-staging`).
2. **Add a MariaDB plugin** -- Railway provisions a managed MariaDB 11.4 instance automatically.
3. **Add a service from GHCR** -- point it at the repository's container image (`ghcr.io/<org>/srv.conta.conspectare`).
4. **Set the root directory** to the repository root (Railway will find `railway.toml` there).

## Required Environment Variables

Set these in the Railway service settings. Refer to `.env.example` for local defaults.

| Variable | Description |
|---|---|
| `ConnectionStrings__ConspectareDb` | MariaDB connection string (use Railway's `${{ MARIADB_* }}` references) |
| `AWS_ACCESS_KEY_ID` | AWS IAM access key for S3 |
| `AWS_SECRET_ACCESS_KEY` | AWS IAM secret key for S3 |
| `AWS_REGION` | AWS region (default: `eu-central-1`) |
| `Aws__BucketName` | S3 bucket name for document storage |
| `Claude__ApiKey` | Anthropic API key for Claude integration |

> **Note:** Do not set `Aws__ServiceUrl` in production -- it is only used locally to point at LocalStack.

## Database Migrations

Migrations are run separately from the API and worker processes using a dedicated entrypoint:

```bash
dotnet Conspectare.Api.dll --migrate
```

In `docker-compose.yml`, the `migrate` service runs this command before `api` and `worker` start. In Railway, configure a one-off deploy command or run migrations as a separate service before the main deployment.

## Health Check

The API exposes `GET /health` (unauthenticated) on port 5100. Railway is configured to probe this endpoint with a 30-second timeout via `railway.toml`.

## S3 Bucket

The S3 bucket must be created manually in AWS:

- Enable default SSE-KMS encryption.
- Block all public access.
- The bucket name must match the `Aws__BucketName` environment variable.

## Dashboard Service

The dashboard frontend lives in a **separate repository**: [`Web.Conta.Conspectare`](https://github.com/bono-ro-ai/Web.Conta.Conspectare).

### Railway Setup

1. **Add a new service from GHCR** in the same Railway project — point it at `ghcr.io/<org>/web.conta.conspectare`.
2. The dashboard serves static files via nginx on port 8080.

### Environment Variables

| Variable | Description |
|---|---|
| `VITE_API_BASE_URL` | URL of the Conspectare API (build-time, baked into JS bundle) |

### Health Check

The dashboard exposes `GET /health` (unauthenticated, returns plain text `ok`). Railway is configured to probe this endpoint with a 30-second timeout via `railway.toml`.

### CI/CD

See `Web.Conta.Conspectare/.github/workflows/ci.yml`.

## Worker Service

The worker container (`Conspectare.WorkerHost`) runs all background workers in a separate process from the API.

### Railway Setup

1. **Add a new service from GHCR** in the same Railway project -- point it at `ghcr.io/<org>/srv.conta.conspectare-worker`.
2. The worker exposes `/health` on port 5101 for health checks.

### Environment Variables

The worker requires the same environment variables as the API (database connection, AWS credentials, LLM keys). Copy the API service's environment variables to the worker service.

### Health Check

The worker exposes `GET /health` (unauthenticated) on port 5101. Configure Railway to probe this endpoint.

## Monitoring

- **Logs:** Railway dashboard > service > Deployments > Logs.
- **Metrics:** Railway dashboard > service > Metrics (CPU, memory, network).
- **Health:** Railway automatically restarts the service on failure (up to 5 retries, configured in `railway.toml`).
