# API Contracts

Last updated: 2026-03-20

All endpoints require `Authorization: Bearer <api_key>` header.

## Documents

### POST `/api/v1/documents`

Upload a document for processing.

**Request**: `multipart/form-data`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | File | Yes | The document file |
| `clientReference` | string | No | Client-provided reference |
| `metadata` | string | No | JSON metadata |

**Headers**:
- `X-Request-Id` (optional) — Idempotency key / external reference

**Response** `202 Accepted`:
```json
{
  "id": 1,
  "externalRef": "uuid",
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

## Authentication

### POST `/api/v1/auth/validate`

Validate an API key.

**Request**:
```json
{
  "apiKey": "dp_test_abc123..."
}
```

**Response** `200 OK`:
```json
{
  "valid": true,
  "clientName": "Test Client"
}
```

**Error Responses**:
- `401 Unauthorized` — Invalid API key

## Health

### GET `/health`

Health check endpoint.

**Response** `200 OK`:
```json
{
  "status": "Healthy"
}
```
