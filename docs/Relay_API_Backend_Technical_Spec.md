# Relay API — Technical Design Specification
**Version:** 1.0  
**Status:** Draft (API-ready)  
**Scope:** Backend “relay” service that sends emergency messages on behalf of the PinTheHighwayCrash PWA and (optionally) enriches location with nearest police/hospital data.

---

## 1) Objectives

- Provide a **single, secure HTTP API** that the PWA can call to deliver emergency messages **without requiring WhatsApp/SMS apps** on the device.
- Support multiple **delivery channels** behind the scenes (SMS, WhatsApp Business API, Email, Voice) with **policy-based fallback**.
- Offer **nearby authority suggestion** (police posts, hospitals) from a local dataset or external APIs.
- Be extensible, observable, and abuse‑resistant (rate limits, idempotency, anti‑spam).

**Non‑goals (v1):**
- End-user UI.
- Citizen identity verification / KYC.
- Public ingestion without authentication.

---

## 2) High-Level Architecture

```
PWA (client) ──HTTPS──> Relay API ──> Channel Providers (SMS/WhatsApp/Email)
                                  └─> Enrichment Providers (Hospitals/Police)
                                  └─> Storage (Messages, Audit, Quotas)
                                  └─> Webhooks (delivery receipts) -> back to API
```

- **Stateless API** behind a load balancer.
- **Background worker(s)** for queued/retryable deliveries.
- **Relational DB** (messages, attempts, quotas, webhooks) + **object store** for attachments (optional).
- **Config** driven routing (per country/provider, quiet hours, costs).

---

## 3) API Surface (v1)

### 3.1 Versions & Base URL
- **Base:** `https://api.example.org/relay`
- **Versioning:** Prefix per major version (`/v1/...`). Backwards-compatible changes use additive fields/headers only.

### 3.2 Authentication
- **Primary:** `Authorization: Bearer <JWT>` (OAuth2 *client credentials* flow).  
  - JWT `aud` = `relay-api`, includes `sub` (client id) and `scope` (e.g., `msg:send`, `lookup:nearby`).
- **Alternative (WASM-friendly):** HMAC scheme
  - `X-Api-Key: <key-id>` and `X-Signature: <base64(HMAC-SHA256(key, method + path + ts + body))>`  
  - `X-Timestamp: <ISO8601 UTC>` valid ±5 min; reject replays (nonce cache).
- **Optional:** mTLS between API gateway and trusted server clients.

### 3.3 Idempotency
- Header: **`Idempotency-Key: <uuid>`** on `POST` endpoints.
- Server stores the first response for 24h and returns it for duplicates (same key + route + body hash).

### 3.4 Rate Limits & Quotas
- **Token bucket** per client (e.g., 60 req/min) + **daily quota** for deliveries.
- Standard headers:
  - `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`
  - On throttle: **429 Too Many Requests** with `Retry-After`.

### 3.5 Error Model
```json
{
  "error": "invalid_request",
  "message": "Readable explanation",
  "code": "MSG_TEMPLATE_MISSING",   // optional machine code
  "correlationId": "a1b2c3..."
}
```
- Always include **`correlationId`** (also in response header `X-Correlation-Id`).

---

## 4) Resources & Endpoints

### 4.1 Message Send (create + dispatch)
`POST /v1/messages`

**Purpose:** Accept an emergency message request, persist, and dispatch to one or more channels with fallback rules.

**Request (example):**
```json
{
  "origin": {
    "appVersion": "1.0.3",
    "deviceHint": "mobile",
    "offlineAttempt": false
  },
  "location": {
    "lat": 19.0760,
    "lng": 72.8777,
    "accuracyMeters": 30,
    "mapsUrl": "https://maps.google.com/?q=19.0760,72.8777"
  },
  "message": {
    "subject": "EMERGENCY: Highway accident",
    "body": "Accident reported. Pinned: 19.0760, 72.8777. Time: 2025-05-10 12:01 UTC",
    "locale": "en-IN"
  },
  "targets": {
    "primary": [
      {"type": "sms", "to": "+91112"},
      {"type": "voice", "to": "+91112"}
    ],
    "secondary": [
      {"type": "email", "to": "controlroom@example.in"},
      {"type": "whatsapp", "to": "+911234567890"}
    ]
  },
  "policy": {
    "strategy": "parallel-primary-then-secondary",
    "requireAnySuccess": true,
    "maxAttemptsPerTarget": 3,
    "backoffSeconds": [10, 30, 120]
  },
  "attachments": [
    {"contentType": "image/jpeg", "url": "https://.../evidence/123.jpg"}
  ],
  "meta": {
    "anonymous": true,
    "spamShieldScore": 0.12,
    "uiFlags": ["no-police-call"],
    "consent": true
  }
}
```

**Response 202 Accepted:**
```json
{
  "id": "msg_01HRZP9Q4T8E6...",
  "status": "queued",
  "acceptedAt": "2025-05-10T12:01:33Z",
  "correlationId": "f9d2e7..."
}
```

**Behavior:**
- Validate auth, quotas, spam checks.
- Persist message + normalized targets.
- Enqueue per‑target delivery jobs.
- Return **202** quickly. Delivery continues asynchronously.

**Status enums:** `queued | sending | partial_success | delivered | failed | canceled`

---

### 4.2 Message Get
`GET /v1/messages/{id}`

Returns full server view:
```json
{
  "id": "msg_...",
  "status": "partial_success",
  "summary": {
    "requestedTargets": 4,
    "attempted": 3,
    "succeeded": 2
  },
  "attempts": [
    {
      "target": {"type": "sms", "to": "+91112"},
      "status": "delivered",
      "providerMessageId": "twilio:SM123",
      "attempts": 1,
      "lastUpdate": "2025-05-10T12:01:40Z"
    },
    {
      "target": {"type": "email", "to": "controlroom@example.in"},
      "status": "failed",
      "error": "smtp_timeout"
    }
  ]
}
```

### 4.3 Message Status (lightweight)
`GET /v1/messages/{id}/status`  
Returns `{ "status": "delivered" }` only.

### 4.4 Suggest Authorities (nearest police & hospitals)
`GET /v1/suggest?lat=..&lng=..&limit=3&types=hospital,police&radius=15000&country=IN`

**Response:**
```json
{
  "query": {"lat": 19.0760, "lng": 72.8777},
  "results": [
    {
      "type": "hospital",
      "name": "XYZ Trauma Center",
      "phone": "+91 22 1234 5678",
      "address": "Road 1, Mumbai",
      "distanceMeters": 1200,
      "source": "json:in-mumbai-2025-05",
      "mapsUrl": "https://maps.google.com/?q=..."
    },
    {
      "type": "police",
      "name": "Sion Police Station",
      "phone": "100",
      "address": "Sion, Mumbai",
      "distanceMeters": 1700,
      "source": "json:in-mumbai-2025-05"
    }
  ]
}
```

### 4.5 Upload Attachment (optional, pre-signed preferred)
`POST /v1/attachments` → returns pre‑signed PUT URL and final CDN URL.
```json
{"uploadUrl": "...", "fileUrl": "https://cdn.../att/abc.jpg", "expiresIn": 300}
```

### 4.6 Health & Metrics
- `GET /v1/health` → `{ status: "ok", uptimeSec: 12345 }`
- `GET /v1/metrics` (Prometheus)
- `GET /v1/info` (build version, git sha)

---

## 5) Delivery Engine

### 5.1 Channels (pluggable)
- **SMS** (e.g., Twilio, MSG91), **WhatsApp Business API**, **Email** (SMTP/SES), **Voice** (text‑to‑speech call).
- Each has a provider adapter with a **uniform interface**: `send()`, `parseReceipt()`, `mapError()`.

### 5.2 Fallback Strategies
- `parallel-primary-then-secondary` (default): fire primary targets in parallel; if none succeed within T, fire secondary.
- `sequential-failover`: try targets in order until one succeeds.
- Each target respects `maxAttemptsPerTarget` and **exponential backoff** `[10, 30, 120, ...]` seconds.

### 5.3 Retries
- **Transient** errors retried with capped backoff (e.g., max 5 min between attempts, max window 30 min).
- **Permanent** errors (e.g., 4xx invalid number) → no retry; mark failed.

### 5.4 Webhooks (Delivery Receipts)
- Providers POST to `POST /v1/webhooks/{provider}`.
- Verify **HMAC signature** (`X-Provider-Signature`) or provider‑specific scheme.
- Map to internal status and upsert attempt record.
- Respond **200** within 1s; heavy work done async.

---

## 6) Anti‑Abuse & Trust

- **CAPTCHA token** (e.g., Turnstile/Recaptcha) accepted in `POST /v1/messages` header `X-Client-Risk-Token` (optional enforcement by client tier).
- **Rate limits** per client + per IP.
- **Quotas** per day/month; emergency whitelists for trusted NGOs.
- **Anonymity flag** stored; **PII minimization** by default.
- **Content guardrails:** basic heuristic spam score; block URLs/domains if needed.
- **Audit log** with redaction (no raw body in high‑detail logs; use hashes).

---

## 7) Data Model (simplified)

- `messages(id, status, subject, body_hash, created_at, client_id, anon, lat, lng, accuracy, locale)`
- `targets(id, message_id, type, address, priority_group)`
- `attempts(id, target_id, provider, status, provider_msg_id, error_code, tries, last_update)`
- `quotas(client_id, day, used_count)`
- `authorities(country, city, dataset_id, name, type, phone, address, lat, lng, updated_at)`

Indexes on `message_id`, `client_id+created_at`, `lat/lng` (spatial).

**Retention:** Attempts 30d, Messages (pseudonymized) 90d, raw body redacted after 7d unless legal hold.

---

## 8) Security

- **HTTPS only**, HSTS, TLS 1.2+.
- **JWT validation**: issuer, audience, exp/skew, kid with JWKS cache.
- **HMAC**: canonical request string, replay window 5 min, nonce store.
- **PII**: encrypt at rest (KMS); field‑level for phone/email; separate keyring per tenant (optional).
- **Secrets**: provider tokens in vault; rotated regularly.
- **CORS**: allow only the PWA origin(s).

---

## 9) Observability

- **Structured logs** (JSON) with `correlationId`, `clientId`, `messageId`.
- **Metrics**: request rate, p95 latency, queue depth, success ratio per channel, error codes.
- **Tracing**: OpenTelemetry spans across API -> worker -> provider.
- **Dashboards** + on‑call alerts for:
  - High 5xx rate
  - Spike in failures per provider
  - Queue latency > SLO

---

## 10) SLOs & Limits

- **API availability:** 99.9%
- **POST /messages latency:** p95 < 300 ms (excluding async delivery)
- **Delivery attempt start:** within 5 s for primary targets
- **Default limits:** 60 req/min, 1000 deliveries/day/client (configurable)

---

## 11) Example Workflows

### 11.1 Happy path
1. PWA posts a message with Idempotency-Key.
2. API authenticates, stores, enqueues attempts for SMS + Voice (primary).
3. SMS delivered → provider webhook → status `delivered`.
4. `/messages/{id}` shows `partial_success` or `delivered` (if policy requires any success).

### 11.2 Offline user (later online)
- PWA queues locally; when online, sends to API same `Idempotency-Key` → server dedupes.

### 11.3 Suggest authorities
- PWA calls `/v1/suggest` with lat/lng → API returns **1–2 nearest hospitals & police** from embedded dataset, falling back to external API if configured.

---

## 12) Request/Response Samples

**POST /v1/messages — Possible 4xx/5xx**
- 400 `invalid_request` (missing body/targets)
- 401 `invalid_token` / 403 `forbidden_scope`
- 409 `idempotency_conflict`
- 413 `payload_too_large`
- 429 `rate_limited` (includes `Retry-After`)
- 500 `internal_error` (with `correlationId`)

**Headers returned (typical):**
```
X-Correlation-Id: f9d2e7...
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 59
X-RateLimit-Reset: 1715347200
```

---

## 13) Provider Adapters (contracts)

```text
Send(request) -> ProviderResponse{ status: accepted|failed, id, raw }
MapWebhook(payload, headers) -> DeliveryEvent{ providerId, targetKey, status, error? }
NormalizeError(raw) -> (isTransient, code, detail)
```

- Adapters run in sandboxed workers; timeouts per provider; circuit breakers around each adapter.

---

## 14) Deployment & Env

- **12‑factor** app; config via env vars or secret store.
- Blue/green or canary deployments; DB migrations via change sets.
- Staging and production keys isolated; test numbers/domains whitelisted.
- Containerized (Docker), K8s HPA based on RPS/queue length.

---

## 15) OpenAPI & SDKs

- OpenAPI 3.1 spec published at `/v1/openapi.json`.
- Generate **C#** and **TypeScript** SDKs.
- Include Postman collection and example curl snippets.

---

## 16) Backward Compatibility

- Only **additive** changes in v1 (new fields optional).
- Breaking changes require `/v2` with parallel support window.

---

## 17) Security Reviews & Compliance (notes)

- Log redaction policy reviewed.
- DPA & data residency config for India/EU if needed.
- Webhook allowlist + signature verification enforced.
- Abuse desk process + emergency revocation of client credentials.

---

## 18) Appendix — Minimal OpenAPI Sketch (excerpt)
```yaml
openapi: 3.1.0
info:
  title: PinTheHighwayCrash Relay API
  version: 1.0.0
servers:
  - url: https://api.example.org/relay
paths:
  /v1/messages:
    post:
      operationId: createMessage
      security:
        - bearerAuth: []
        - hmacAuth: []
      parameters:
        - in: header
          name: Idempotency-Key
          schema: { type: string, format: uuid }
      responses:
        "202": { description: Accepted }
        "400": { description: Bad Request }
        "401": { description: Unauthorized }
        "429": { description: Rate Limited }
  /v1/messages/{id}:
    get:
      operationId: getMessage
      parameters:
        - in: path
          name: id
          required: true
          schema: { type: string }
      responses:
        "200": { description: Ok }
  /v1/suggest:
    get:
      operationId: suggestAuthorities
      parameters:
        - in: query
          name: lat
          schema: { type: number }
          required: true
        - in: query
          name: lng
          schema: { type: number }
          required: true
        - in: query
          name: limit
          schema: { type: integer, default: 2, maximum: 5 }
      responses:
        "200": { description: Ok }
components:
  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
    hmacAuth:
      type: apiKey
      in: header
      name: X-Api-Key
```

---

### END
