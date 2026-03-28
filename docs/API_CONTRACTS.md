# API Contracts

Last updated: 2026-03-28

All endpoints are prefixed with the API host URL. Authentication is required unless marked as anonymous.

## Authentication

Two schemes supported (auto-detected by token shape):
- **API Key**: `Authorization: Bearer <opaque-key>` (no dots in token)
- **JWT**: `Authorization: Bearer <jwt-token>` (contains dots)

---

## 1. Health

### `GET /health`

**Auth**: None (anonymous)

**Response** `200 OK` / `503 Service Unavailable`:
```json
{
  "status": "healthy | degraded",
  "database": "ok | error",
  "storage": "ok | error"
}
```

---

## 2. Auth (`/api/v1/auth`)

### `POST /api/v1/auth/login`

**Auth**: Anonymous

**Request**:
```json
{
  "email": "string",
  "password": "string"
}
```

**Response** `200 OK`:
```json
{
  "token": "string (JWT)",
  "expiresAt": "datetime",
  "user": {
    "id": "long",
    "email": "string",
    "name": "string",
    "role": "string",
    "avatarUrl": "string?"
  }
}
```

Sets `refreshToken` HttpOnly cookie.

### `POST /api/v1/auth/google`

**Auth**: Anonymous

**Request**:
```json
{
  "credential": "string (Google ID token)"
}
```

**Response**: Same as `/login`.

### `GET /api/v1/auth/google/redirect`

**Auth**: Anonymous

Redirects to Google OAuth consent screen. Returns `302`.

### `POST /api/v1/auth/google/callback`

**Auth**: Anonymous

**Request**:
```json
{
  "code": "string (authorization code)",
  "state": "string (HMAC-signed state token)"
}
```

**Response**: Same as `/login`.

### `POST /api/v1/auth/signup`

**Auth**: Anonymous

**Request**:
```json
{
  "companyName": "string",
  "cui": "string?",
  "email": "string",
  "password": "string (min 10 chars, upper+lower+digit)"
}
```

**Response** `201 Created`:
```json
{
  "tenantId": "long",
  "userId": "long",
  "email": "string",
  "role": "string",
  "apiKey": "string (plain, shown once)",
  "apiKeyPrefix": "string (8 chars)",
  "trialExpiresAt": "datetime",
  "token": "string (JWT)"
}
```

### `POST /api/v1/auth/register`

**Auth**: JWT (admin only)

**Request**:
```json
{
  "email": "string",
  "name": "string",
  "password": "string (min 10 chars, upper+lower+digit)"
}
```

**Response** `201 Created`:
```json
{
  "message": "Registration successful."
}
```

### `POST /api/v1/auth/refresh`

**Auth**: Anonymous (uses `refreshToken` cookie)

**Response** `200 OK`: Same shape as `/login`. Rotates refresh token.

### `POST /api/v1/auth/revoke`

**Auth**: JWT

**Response** `204 No Content`. Revokes all refresh tokens for the user.

### `POST /api/v1/auth/magic-link/send`

**Auth**: Anonymous. Rate-limited: 5 requests per 15 minutes per IP.

**Request**:
```json
{
  "email": "string"
}
```

**Response** `200 OK`:
```json
{
  "message": "string"
}
```

### `POST /api/v1/auth/magic-link/verify`

**Auth**: Anonymous

**Request**:
```json
{
  "token": "string"
}
```

**Response**: Same as `/login`.

### `GET /api/v1/auth/me`

**Auth**: JWT

**Response** `200 OK`:
```json
{
  "id": "long",
  "email": "string",
  "name": "string",
  "role": "string",
  "avatarUrl": "string?"
}
```

---

## 3. Documents (`/api/v1/documents`)

### `POST /api/v1/documents`

**Auth**: Required

Upload a single file. `multipart/form-data`. Max 50 MB.

**Headers**:
- `X-Request-Id`: External reference (optional)

**Form fields**:
- `file`: The file (required)
- `clientReference`: Client reference string (optional)
- `metadata`: Arbitrary metadata string (optional)
- `fiscalCode`: Fiscal code / CUI (optional)

**Allowed content types**: `text/xml`, `application/xml`, `application/pdf`, `image/jpeg`, `image/png`, `image/tiff`, `image/heic`, `image/webp`, `application/json`, `text/csv`, `application/octet-stream`

**Response** `202 Accepted`:
```json
{
  "id": "long",
  "documentRef": "string",
  "status": "processing",
  "createdAt": "datetime"
}
```

### `POST /api/v1/documents/batch`

**Auth**: Required

Upload up to 20 files. `multipart/form-data`. Max 200 MB total.

**Headers**:
- `X-Request-Id`: External reference (appended with `:index`)

**Form fields**:
- `files`: Multiple files (required, 1-20)
- `fiscalCode`, `clientReference`, `metadata`: Same as single upload

**Response** `202 Accepted` (all success) / `207 Multi-Status` (partial failure):
```json
{
  "results": [
    {
      "index": "int",
      "fileName": "string",
      "id": "long?",
      "documentRef": "string?",
      "status": "string?",
      "error": "string?",
      "statusCode": "int"
    }
  ],
  "total": "int",
  "succeeded": "int",
  "failed": "int"
}
```

### `GET /api/v1/documents`

**Auth**: Required

**Query params**:
- `status`: Filter by external status
- `search`: Full-text search
- `dateFrom`, `dateTo`: Date range filter
- `page`: Page number (default 1)
- `pageSize`: Items per page (default 20)

**Response** `200 OK`:
```json
{
  "items": [
    {
      "id": "long",
      "documentRef": "string?",
      "fileName": "string",
      "documentType": "string?",
      "status": "string (external)",
      "createdAt": "datetime",
      "completedAt": "datetime?"
    }
  ],
  "totalCount": "int",
  "page": "int",
  "pageSize": "int"
}
```

### `GET /api/v1/documents/{id}`

**Auth**: Required

**Response** `200 OK`: Full document detail including canonical output, extraction attempts, review flags, and events.

### `GET /api/v1/documents/{id}/raw`

**Auth**: Required

Streams the original uploaded file from S3. Sets `Content-Disposition: inline`.

### `POST /api/v1/documents/{id}/retry`

**Auth**: Required

Re-queues a failed document for processing.

**Response** `200 OK`: Full document detail.

### `POST /api/v1/documents/{id}/resolve`

**Auth**: Required

Resolves a document pending human review.

**Request**:
```json
{
  "action": "approve | reject",
  "canonicalOutputJson": "string? (corrected JSON)"
}
```

**Response** `200 OK`: Full document detail.

### `PATCH /api/v1/documents/{id}/canonical-output`

**Auth**: Required

Updates the canonical output JSON without changing document status.

**Request**:
```json
{
  "canonicalOutputJson": "string"
}
```

**Response** `200 OK`: Full document detail.

---

## 4. Review Queue (`/api/v1/admin/review-queue`)

All endpoints require admin access.

### `GET /api/v1/admin/review-queue`

**Query params**: `page` (default 1), `pageSize` (default 50, max 200)

**Response** `200 OK`:
```json
{
  "items": [{ "...review queue item..." }],
  "totalCount": "int",
  "hasNextPage": "bool",
  "page": "int",
  "pageSize": "int"
}
```

### `GET /api/v1/admin/review-queue/{id}`

Returns full review detail with pre-signed S3 URL (15min) and canonical output JSON.

### `POST /api/v1/admin/review-queue/{id}/approve`

**Request**:
```json
{
  "notes": "string?"
}
```

### `POST /api/v1/admin/review-queue/{id}/reject`

**Request**:
```json
{
  "reason": "string (required)"
}
```

---

## 5. Dashboard (`/api/v1/dashboard`)

All endpoints require authentication.

### `GET /api/v1/dashboard/queue-depths`

**Response** `200 OK`:
```json
{
  "items": [{ "status": "string", "count": "int" }],
  "total": "int"
}
```

### `GET /api/v1/dashboard/processing-times`

**Query params**: `from`, `to` (optional, defaults to last 30 days)

**Response** `200 OK`:
```json
{
  "p50": "double",
  "p95": "double",
  "sampleCount": "int",
  "from": "datetime",
  "to": "datetime"
}
```

### `GET /api/v1/dashboard/error-rates`

**Query params**: `from`, `to`

**Response** `200 OK`:
```json
{
  "total": "int",
  "failed": "int",
  "errorRate": "decimal",
  "from": "datetime",
  "to": "datetime"
}
```

### `GET /api/v1/dashboard/llm-costs`

**Query params**: `from`, `to`

**Response** `200 OK`:
```json
{
  "items": [
    {
      "modelId": "string",
      "totalInputTokens": "long",
      "totalOutputTokens": "long",
      "attemptCount": "int"
    }
  ],
  "grandInputTokens": "long",
  "grandOutputTokens": "long",
  "from": "datetime",
  "to": "datetime"
}
```

### `GET /api/v1/dashboard/volumes`

**Query params**: `from`, `to`

**Response** `200 OK`:
```json
{
  "items": [{ "date": "datetime", "count": "int" }],
  "total": "int",
  "from": "datetime",
  "to": "datetime"
}
```

---

## 6. Admin API Clients (`/api/v1/admin/api-clients`)

All endpoints require admin access.

### `POST /api/v1/admin/api-clients`

**Request**:
```json
{
  "name": "string (max 200)",
  "rateLimitPerMin": "int (> 0)",
  "maxFileSizeMb": "int (> 0)",
  "webhookUrl": "string? (valid HTTP/HTTPS URI)"
}
```

**Response** `201 Created`:
```json
{
  "id": "long",
  "name": "string",
  "apiKeyPrefix": "string",
  "plainKey": "string (shown once)",
  "createdAt": "datetime"
}
```

### `GET /api/v1/admin/api-clients`

**Response** `200 OK`: Array of API client list items.

### `DELETE /api/v1/admin/api-clients/{id}`

**Response** `204 No Content`. Soft-deletes the client.

---

## 7. Admin Usage (`/api/v1/admin/usage`)

All endpoints require admin access.

### `GET /api/v1/admin/usage`

**Query params**: `tenantId` (required), `from` (required), `to` (required)

**Response** `200 OK`:
```json
{
  "items": [
    {
      "usageDate": "datetime",
      "documentsIngested": "int",
      "documentsProcessed": "int",
      "llmInputTokens": "long",
      "llmOutputTokens": "long",
      "llmRequests": "int",
      "storageBytes": "long",
      "apiCalls": "int"
    }
  ],
  "tenantId": "long",
  "from": "datetime",
  "to": "datetime"
}
```

### `GET /api/v1/admin/usage/monthly`

**Query params**: `tenantId` (required), `from` (required), `to` (required)

**Response** `200 OK`: Monthly aggregated usage summaries.

---

## 8. Tenant Settings (`/api/v1/tenant/settings`)

All endpoints require authentication.

### `GET /api/v1/tenant/settings`

**Response** `200 OK`:
```json
{
  "tenantId": "long",
  "companyName": "string?",
  "cui": "string?",
  "contactEmail": "string?",
  "webhookUrl": "string?",
  "hasWebhookSecret": "bool",
  "apiKeyPrefix": "string?",
  "trialExpiresAt": "datetime?",
  "isTrialActive": "bool"
}
```

### `PUT /api/v1/tenant/settings`

**Request**:
```json
{
  "companyName": "string?",
  "cui": "string?",
  "webhookUrl": "string?",
  "webhookSecret": "string?"
}
```

**Response**: Same as `GET`.

### `POST /api/v1/tenant/settings/rotate-api-key`

**Response** `200 OK`:
```json
{
  "plainApiKey": "string (shown once)",
  "apiKeyPrefix": "string"
}
```

---

## 9. Prometheus Metrics

### `GET /metrics`

**Auth**: None. Exposed on both API (:5100) and Worker (:5101).

Returns OpenTelemetry/Prometheus text format with custom `Conspectare` meter metrics.
