# Integrations & Crons

Last updated: 2026-03-22

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

## II. Background Workers

See `CLAUDE.md` section on Workers. Workers run as `IHostedService` inside the API host process.

## III. Cron Jobs

None currently configured.

## IV. Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `PNL_API_URL` | No | `http://localhost:5200` | Base URL for the P&L Expense Tracker service. Used to construct the webhook callback URL during ApiClient seed migration. |
