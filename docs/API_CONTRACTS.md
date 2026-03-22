# API Contracts

Last updated: 2026-03-22

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

## Health

### GET `/health`

Health check endpoint.

**Response** `200 OK`:
```json
{
  "status": "Healthy"
}
```
