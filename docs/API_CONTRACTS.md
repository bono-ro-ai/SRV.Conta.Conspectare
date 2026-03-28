# API Contracts

Last updated: 2026-03-28

## API Surface Classification

| Category | Endpoints | Auth Required |
|----------|-----------|---------------|
| **PUBLIC** | `POST /api/v1/auth/signup`, `POST /api/v1/auth/magic-link/send`, `POST /api/v1/auth/magic-link/verify` | No (anonymous) |
| **TENANT API** | `POST /api/v1/documents`, `POST /api/v1/documents/batch`, `GET /api/v1/documents`, `GET /api/v1/documents/{id}`, `GET /api/v1/documents/{id}/raw`, `POST /api/v1/documents/{id}/retry`, `POST /api/v1/documents/{id}/resolve`, `PATCH /api/v1/documents/{id}/canonical-output`, `GET /api/v1/dashboard/*`, `GET /api/v1/tenant/settings`, `PUT /api/v1/tenant/settings`, `POST /api/v1/tenant/settings/rotate-api-key`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me` | Yes (API key or JWT) |
| **ADMIN** | `POST /api/v1/admin/api-clients`, `GET /api/v1/admin/api-clients`, `DELETE /api/v1/admin/api-clients/{id}`, `GET /api/v1/admin/usage`, `GET /api/v1/admin/usage/monthly`, `GET /api/v1/admin/review-queue`, `GET /api/v1/admin/review-queue/{id}`, `POST /api/v1/admin/review-queue/{id}/approve`, `POST /api/v1/admin/review-queue/{id}/reject` | Yes (auth + IsAdmin) |
| **SYSTEM** | `GET /health` | No (anonymous) |

## Documents

### POST `/api/v1/documents/batch`

Upload multiple documents in a single request. Returns per-file results with partial success support.

**Request**: `multipart/form-data`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `files` | File[] | Yes | 1–20 document files |
| `clientReference` | string | No | Client-provided reference (shared across all files) |
| `metadata` | string | No | JSON metadata (shared across all files) |
| `fiscalCode` | string | No | Fiscal code (CUI) of the issuer (shared across all files) |

**Headers**:
- `X-Request-Id` (optional) — Base idempotency key. Per-file refs are generated as `{requestId}:{index}`.

**Response** `202 Accepted` (all files succeeded) / `207 Multi-Status` (any file failed):
```json
{
  "results": [
    {
      "index": 0,
      "fileName": "invoice1.pdf",
      "id": 1,
      "documentRef": "12345678-26-1",
      "status": "processing",
      "error": null,
      "statusCode": 201
    },
    {
      "index": 1,
      "fileName": "bad.exe",
      "id": null,
      "documentRef": null,
      "status": null,
      "error": "Content type 'application/x-msdownload' is not supported.",
      "statusCode": 400
    }
  ],
  "totalFiles": 2,
  "succeeded": 1,
  "failed": 1
}
```

**Error Responses**:
- `400 Bad Request` — No files provided or more than 20 files

**Limits**:
- Maximum 20 files per batch
- Maximum 200 MB total request size
- Per-file size limit governed by tenant `MaxFileSizeMb` setting
- Per-file content type validation (same rules as single upload)

### POST `/api/v1/documents`

Upload a document for processing.

**Request**: `multipart/form-data`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File | Yes | The document file |
| `clientReference` | string | No | Client-provided reference |
| `metadata` | string | No | JSON metadata |
| `fiscalCode` | string | No | Fiscal code (CUI) of the issuer. Used to generate `documentRef` in format `{CUI}-{YY}-{N}`. "RO" prefix is stripped automatically. Defaults to "007" if empty. |

**Headers**:
- `X-Request-Id` (optional) — Idempotency key / external reference

**Response** `202 Accepted`:
```json
{
  "id": 1,
  "documentRef": "12345678-26-1",
  "status": "processing",
  "createdAt": "2026-03-20T10:00:00Z"
}
```

### GET `/api/v1/documents`

List documents with optional filters. Returns paginated results.

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | string | — | Filter by external status (comma-separated) |
| `search` | string | — | Search by external ref or file name |
| `dateFrom` | DateTime | — | Filter documents created after this date |
| `dateTo` | DateTime | — | Filter documents created before this date |
| `page` | int | 1 | Page number |
| `pageSize` | int | 20 | Items per page |

**Response** `200 OK`:
```json
{
  "items": [
    {
      "id": 1,
      "documentRef": "12345678-26-1",
      "fiscalCode": "12345678",
      "externalRef": "uuid",
      "fileName": "invoice.xml",
      "contentType": "text/xml",
      "fileSizeBytes": 1024,
      "status": "processing",
      "documentType": "invoice",
      "isTerminal": false,
      "createdAt": "2026-03-20T10:00:00Z",
      "updatedAt": "2026-03-20T10:00:00Z",
      "completedAt": null
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

### GET `/api/v1/documents/{id}`

Retrieve a single document with full details including events, extraction attempts, and review flags.

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Document ID |

**Response** `200 OK`:
```json
{
  "id": 1,
  "documentRef": "12345678-26-1",
  "fiscalCode": "12345678",
  "externalRef": "uuid",
  "fileName": "invoice.xml",
  "contentType": "text/xml",
  "fileSizeBytes": 1024,
  "inputFormat": "xml_efactura",
  "status": "processing",
  "documentType": "invoice",
  "triageConfidence": 0.95,
  "isAccountingRelevant": true,
  "retryCount": 0,
  "maxRetries": 3,
  "errorMessage": null,
  "clientReference": "client-ref-001",
  "metadata": "{}",
  "canonicalOutputJson": null,
  "reviewFlags": [
    {
      "id": 1,
      "flagType": "confidence_low",
      "severity": "warning",
      "message": "Low extraction confidence",
      "isResolved": false,
      "resolvedAt": null,
      "createdAt": "2026-03-20T10:00:00Z"
    }
  ],
  "events": [
    {
      "id": 1,
      "eventType": "status_change",
      "fromStatus": "pending_triage",
      "toStatus": "triaging",
      "details": null,
      "createdAt": "2026-03-20T10:00:00Z"
    }
  ],
  "extractionAttempts": [
    {
      "id": 1,
      "attemptNumber": 1,
      "phase": "extraction",
      "modelId": "gpt-4",
      "promptVersion": "v2.1",
      "status": "completed",
      "inputTokens": 500,
      "outputTokens": 200,
      "latencyMs": 1200,
      "confidence": 0.95,
      "errorMessage": null,
      "createdAt": "2026-03-20T10:00:00Z",
      "completedAt": "2026-03-20T10:00:02Z"
    }
  ],
  "isTerminal": false,
  "createdAt": "2026-03-20T10:00:00Z",
  "updatedAt": "2026-03-20T10:00:00Z",
  "completedAt": null
}
```

**Error Responses**:
- `404 Not Found` — Document does not exist or belongs to another tenant

### GET `/api/v1/documents/{id}/raw`

Download the raw document file.

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Document ID |

**Response** `200 OK`: Binary file stream with original `Content-Type`.

**Error Responses**:
- `404 Not Found` — Document does not exist or belongs to another tenant

### POST `/api/v1/documents/{id}/retry`

Retry processing a failed document. Only allowed for documents in a failed state that have not exceeded max retries.

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Document ID |

**Response** `200 OK`: Returns the updated `DocumentResponse` (same schema as GET `/api/v1/documents/{id}`).

**Error Responses**:
- `400 Bad Request` — Maximum retry count exceeded
- `404 Not Found` — Document does not exist or belongs to another tenant
- `409 Conflict` — Document is not in a retryable state

### POST `/api/v1/documents/{id}/resolve`

Manually resolve a document in `review_required` status.

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Document ID |

**Request Body** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `action` | string | Yes | One of: `confirm`, `provide_corrected`, `reject` |
| `canonicalOutputJson` | string | Conditional | Required when action is `provide_corrected`. The corrected canonical output JSON. |

**Actions**:
- `confirm` — Accept current extraction as-is, mark review flags resolved, transition to `completed`
- `provide_corrected` — Provide corrected canonical output JSON, update extraction, mark review flags resolved, transition to `completed`
- `reject` — Reject the document, mark review flags resolved, transition to `rejected`

**Request Examples**:

Confirm:
```json
{
  "action": "confirm",
  "canonicalOutputJson": null
}
```

Provide corrected:
```json
{
  "action": "provide_corrected",
  "canonicalOutputJson": "{\"invoiceNumber\": \"INV-001\", ...}"
}
```

Reject:
```json
{
  "action": "reject",
  "canonicalOutputJson": null
}
```

**Response** `200 OK`: Returns the updated `DocumentResponse` (same schema as GET `/api/v1/documents/{id}`).

**Error Responses**:
- `400 Bad Request` — Invalid action or missing `canonicalOutputJson` for `provide_corrected`
- `404 Not Found` — Document does not exist or belongs to another tenant
- `409 Conflict` — Document is not in `review_required` status

### PATCH `/api/v1/documents/{id}/canonical-output`

Edit the canonical output of a document in `review_required` status without triggering a status transition.

**Path Parameters**:

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | long | Document ID |

**Request Body** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `canonicalOutputJson` | string | Yes | The updated canonical output JSON |

**Request Example**:
```json
{
  "canonicalOutputJson": "{\"invoiceNumber\": \"INV-001\", \"totalAmount\": 100.50}"
}
```

**Response** `200 OK`: Returns the updated `DocumentResponse` (same schema as GET `/api/v1/documents/{id}`).

**Behavior**:
- Updates the canonical output JSON and re-indexes searchable fields (`invoiceNumber`, `issueDate`, `dueDate`, `supplierCui`, `customerCui`, `currency`, `totalAmount`, `vatAmount`)
- Creates a `canonical_output_edited` audit event
- Does **not** change document status — remains in `review_required`

**Error Responses**:
- `400 Bad Request` — Missing or invalid JSON in `canonicalOutputJson`
- `404 Not Found` — Document does not exist or belongs to another tenant
- `409 Conflict` — Document is not in `review_required` status, or has no canonical output

## Authentication

### POST `/api/v1/auth/signup`

Self-service tenant signup. Creates a new API client (tenant), user, and API key.

**Request** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `companyName` | string | Yes | Company name |
| `cui` | string | No | CUI/CIF (RO prefix stripped automatically) |
| `email` | string | Yes | Contact email (becomes the user's login) |
| `password` | string | Yes | Min 10 chars, 1 upper, 1 lower, 1 digit |

**Response** `201 Created`:
```json
{
  "tenantId": 1,
  "userId": 1,
  "email": "user@example.com",
  "role": "user",
  "apiKey": "csp_abc123...",
  "apiKeyPrefix": "csp_abc1",
  "trialExpiresAt": "2026-04-21T10:00:00Z",
  "token": "eyJ...",
  "refreshToken": "abc123..."
}
```

**Error Responses**:
- `400 Bad Request` — Missing required fields or weak password
- `409 Conflict` — Email already registered

### POST `/api/v1/auth/magic-link/send`

Request a magic link for passwordless authentication. Creates a new user if the email is not registered.

**Request** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `email` | string | Yes | Email address |

**Response** `200 OK`:
```json
{
  "message": "Dacă există un cont cu acest email, un link de autentificare a fost trimis."
}
```

**Behavior**:
- If no user exists with the given email, one is auto-created (first user gets `admin` role, subsequent get `user` role, `passwordHash` is null)
- Generates a 32-byte random token, hashes with SHA-256, stores in `sec_magic_link_tokens`
- Sends email via Mandrill with a link to `{FrontendUrl}/auth/magic-link?token={rawToken}`
- Token expires in 15 minutes

**Error Responses**:
- `400 Bad Request` — Missing email

### POST `/api/v1/auth/magic-link/verify`

Verify a magic link token and return JWT + refresh token.

**Request** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `token` | string | Yes | Raw token from the magic link URL |

**Response** `200 OK`:
```json
{
  "token": "eyJ...",
  "expiresAt": "2026-03-22T10:15:00Z",
  "user": {
    "id": 1,
    "email": "user@example.com",
    "name": "user",
    "role": "admin"
  }
}
```

**Behavior**:
- Hashes the raw token and looks up in `sec_magic_link_tokens`
- Validates: token exists, not already used, not expired
- Marks token as used (`used_at` = now)
- Updates user `last_login_at`
- Returns JWT access token + sets refresh token cookie

**Error Responses**:
- `400 Bad Request` — Missing token, invalid/expired/already-used token

### GET `/api/v1/auth/google/redirect`

Initiates the Google OAuth 2.0 Authorization Code flow. Redirects the browser to Google's consent screen.

**Behavior**:
- Sets a `google_oauth_state` HttpOnly cookie (CSRF protection, 10-minute TTL)
- Redirects to `accounts.google.com/o/oauth2/v2/auth` with `response_type=code`, `scope=openid profile email`
- Google redirects back to `{FrontendUrl}/auth/google/callback?code=...&state=...`

**Response**: `302 Redirect` to Google

### POST `/api/v1/auth/google/callback`

Exchanges a Google authorization code for tokens, validates the ID token, and authenticates the user.

**Request** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `code` | string | Yes | Authorization code from Google redirect |
| `state` | string | Yes | CSRF state parameter from Google redirect |

**Response** `200 OK`:
```json
{
  "token": "eyJ...",
  "expiresAt": "2026-03-28T10:15:00Z",
  "user": {
    "id": 1,
    "email": "user@bono.ro",
    "name": "User Name",
    "role": "admin",
    "avatarUrl": "https://..."
  }
}
```

**Behavior**:
- Validates `state` against the `google_oauth_state` cookie
- Exchanges `code` for tokens via `https://oauth2.googleapis.com/token`
- Extracts and validates the `id_token` using Google's public keys
- Delegates to the same user lookup/creation logic as `POST /api/v1/auth/google`
- Sets refresh token HttpOnly cookie

**Error Responses**:
- `400 Bad Request` — Missing code/state, invalid state, code exchange failure
- `401 Unauthorized` — Invalid Google token or unverified email
- `403 Forbidden` — Email not in allowed domain or Google group

## Tenant Settings

### GET `/api/v1/tenant/settings`

Returns current tenant configuration.

**Response** `200 OK`:
```json
{
  "tenantId": 1,
  "companyName": "Test Company",
  "cui": "12345678",
  "contactEmail": "user@example.com",
  "webhookUrl": "https://example.com/webhook",
  "hasWebhookSecret": true,
  "apiKeyPrefix": "csp_abc1",
  "trialExpiresAt": "2026-04-21T10:00:00Z",
  "isTrialActive": true
}
```

### PUT `/api/v1/tenant/settings`

Update tenant settings. All fields are optional (null = no change).

**Request** `application/json`:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `companyName` | string | No | Updated company name |
| `cui` | string | No | Updated CUI (RO prefix stripped automatically) |
| `webhookUrl` | string | No | Webhook URL |
| `webhookSecret` | string | No | Webhook secret |

**Response** `200 OK`: Same schema as GET.

### POST `/api/v1/tenant/settings/rotate-api-key`

Generate a new API key, invalidating the previous one.

**Response** `200 OK`:
```json
{
  "apiKey": "csp_new123...",
  "apiKeyPrefix": "csp_new1"
}
```

## Webhook Events

When a document reaches a terminal or review-required status, a webhook is dispatched to the API client's configured `webhookUrl`.

### Payload Schema

```json
{
  "event": "document.status_changed",
  "document_id": 42,
  "document_ref": "12345678-26-1",
  "fiscal_code": "12345678",
  "external_ref": "uuid-or-null",
  "status": "completed",
  "timestamp": "2026-03-20T12:00:00.0000000Z",
  "client_reference": "client-ref-or-null",
  "document_type": "invoice",
  "confidence": 0.95,
  "completed_at": "2026-03-20T14:30:00.0000000Z",
  "result_summary": { "invoice_number": "FAC-001", "total_amount": 1190.00 },
  "canonical_output_json": "{\"invoice_number\":\"FAC-001\",\"total_amount\":1190.00}",
  "error_message": null,
  "review_flags": [
    {
      "flag_type": "confidence_low",
      "severity": "warning",
      "message": "Low extraction confidence",
      "is_resolved": false
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `event` | string | Always `document.status_changed` |
| `document_id` | long | Document ID |
| `document_ref` | string? | Auto-generated document reference in `{CUI}-{YY}-{N}` format |
| `fiscal_code` | string? | Normalized fiscal code (CUI without RO prefix) |
| `external_ref` | string? | External reference (from upload or X-Request-Id) |
| `status` | string | Document status: `completed`, `failed`, `rejected`, `review_required` |
| `timestamp` | string | ISO 8601 UTC timestamp of the event |
| `client_reference` | string? | Client-provided reference from upload |
| `document_type` | string? | Detected document type (e.g., `invoice`) |
| `confidence` | decimal? | Triage confidence score (0.0 - 1.0) |
| `completed_at` | string? | ISO 8601 UTC timestamp when document completed |
| `result_summary` | object? | Parsed canonical output (structured JSON) |
| `canonical_output_json` | string? | Raw canonical output JSON string |
| `error_message` | string? | Error message (present when status is `failed`) |
| `review_flags` | array? | Review flags (present when explicitly passed) |

### Webhook Signature (X-Webhook-Signature)

When an API client has a `webhookSecret` configured, each webhook delivery includes an `X-Webhook-Signature` header for payload verification.

**Signature format**: `sha256=<lowercase hex HMAC-SHA256 digest>`

**Computation**: HMAC-SHA256 of the raw JSON payload body using the webhook secret as key.

**Verification example** (pseudocode):
```
expected = "sha256=" + hex(hmac_sha256(webhook_secret, raw_body))
actual = request.headers["X-Webhook-Signature"]
secure_compare(expected, actual)
```

When no webhook secret is configured, the `X-Webhook-Signature` header is omitted.

## Admin — Usage Tracking

### GET `/api/v1/admin/usage`

Get daily usage metrics for a tenant. Admin access required (requires `[Authorize]`).

**Field semantics**:
- `storageBytes` — bytes of artifacts created on that day (incremental, not cumulative)
- `apiCalls` — count of document ingestion API calls (v1 approximation; does not include read-only API calls)

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tenantId` | long | Yes | Tenant (API client) ID |
| `from` | DateTime | Yes | Start date (inclusive) |
| `to` | DateTime | Yes | End date (inclusive) |

**Response** `200 OK`:
```json
{
  "items": [
    {
      "usageDate": "2026-03-21",
      "documentsIngested": 10,
      "documentsProcessed": 8,
      "llmInputTokens": 5000,
      "llmOutputTokens": 3000,
      "llmRequests": 12,
      "storageBytes": 1024000,
      "apiCalls": 10
    }
  ],
  "tenantId": 1,
  "from": "2026-03-01T00:00:00Z",
  "to": "2026-03-22T00:00:00Z"
}
```

**Error Responses**:
- `400 Bad Request` — Missing or invalid parameters
- `403 Forbidden` — Non-admin API key

### GET `/api/v1/admin/usage/monthly`

Get monthly usage summary for a tenant. Admin access required.

**Query Parameters**:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `tenantId` | long | Yes | Tenant (API client) ID |
| `from` | DateTime | Yes | Start date (inclusive) |
| `to` | DateTime | Yes | End date (inclusive) |

**Response** `200 OK`:
```json
{
  "items": [
    {
      "year": 2026,
      "month": 3,
      "documentsIngested": 150,
      "documentsProcessed": 120,
      "llmInputTokens": 75000,
      "llmOutputTokens": 45000,
      "llmRequests": 180,
      "storageBytes": 15360000,
      "apiCalls": 150
    }
  ],
  "tenantId": 1,
  "from": "2026-01-01T00:00:00Z",
  "to": "2026-03-31T00:00:00Z"
}
```

**Error Responses**:
- `400 Bad Request` — Missing or invalid parameters
- `403 Forbidden` — Non-admin API key

## Health

### GET `/health`

Health check endpoint.

**Response** `200 OK`:
```json
{
  "status": "Healthy"
}
```
