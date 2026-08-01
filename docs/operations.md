# Operations

## Current endpoints

- `/health/live` reports whether the process can serve requests and intentionally runs no dependency
  checks.
- `/health/ready` verifies PostgreSQL connectivity and, when enabled, RabbitMQ and Redis
  connectivity. It reports unhealthy when a required dependency is unavailable.

## Local commands

```bash
dotnet run --project src/WorkOps.Api
docker compose config
docker compose up --build
docker compose down
```

Standalone hosts leave `Messaging:Enabled` false unless a broker is deliberately configured.
Docker Compose enables RabbitMQ, applies the database migration, starts the leased outbox worker,
starts the notification consumer, and enables authenticated Redis caching. It also enables a local
clean-scanner stub and stores attachments in `/tmp/workops-attachments` inside the API container.
That directory is temporary and is not a production durability design.

The local Keycloak realm contains four synthetic demo users and an audience mapper for the
`workops-cli` client. `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` lets the API fetch discovery and JWKS data
through the private Compose hostname while validating the public local issuer. This password-grant
realm exists only for the local scripted demo and must not be promoted to a deployment.

## Cache and attachment configuration

`Cache:Enabled` is false by default. When enabled, `ConnectionStrings:Redis` is required. Cache
outages fall back to PostgreSQL for entitlement reads and never bypass the PostgreSQL quota.

`Files:RootPath` must be absolute. `Files:DevelopmentScannerEnabled` is false by default, causing
uploads to fail closed with `503`. Compose enables the stub only for local demonstration. A deployed
environment must provide private durable storage, backups/retention, malware scanning, and alerts
for missing objects or scanner failures before attachments are considered production-ready.

## Message delivery and recovery

Outbox messages move through `Pending`, `Processing`, `Processed`, and `Failed`. A worker claims one
eligible row with a 30-second lease and PostgreSQL `FOR UPDATE SKIP LOCKED`. Publish failures use
deterministic exponential backoff with jitter and stop after five attempts. Only a generic failure
code is stored; exception text and broker credentials are never persisted.

RabbitMQ deliveries use manual acknowledgments and prefetch one. The consumer retries a transient
handler failure once, then routes a repeated or invalid delivery to
`workops.notifications.failed.v1`. The handler writes its inbox receipt and development
notification in one database transaction, so a redelivery with the same tenant/message/consumer
key has no additional effect.

An owner or administrator may replay a known failed outbox message with:

```text
POST /api/v1/operations/outbox/{messageId}/replay
X-Workspace-Id: {workspaceId}
```

The endpoint cannot replay pending, processing, or processed messages. Every accepted replay writes
an audit event. Failed-queue inspection and republishing remain restricted broker-operator tasks;
message bodies and credentials must not be copied into tickets or logs.

The current code emits low-cardinality message result, outbox duration, and outbox backlog metrics.

## Diagnostics and HTTP controls

Every response returns `X-Correlation-Id`; Problem Details also include `correlationId` and
`traceId`. Search the structured JSON logs using that value, then follow the trace through ASP.NET
Core, outbound HTTP, PostgreSQL, and `WorkOps.Messaging`. Audit and message rows retain the same
server-generated correlation identifier where the business transaction records one.

Set `Observability:Otlp:Enabled=true` and an absolute HTTP(S) `Observability:Otlp:Endpoint` to export
traces and metrics. The application reports request and runtime metrics plus low-cardinality cache,
message result, outbox duration, and outbox backlog instruments. Do not add user IDs, email, tokens,
work-item titles, raw URLs, or submitted values as labels.

Rate limiting defaults to 60 requests per 60 seconds per authenticated subject, or per remote IP
before authentication. Health endpoints are exempt. `Cors:AllowedOrigins` is empty by default;
production origins must be explicit HTTPS origins. OpenAPI JSON is available only in Development at
`/openapi/v1.json`; no interactive UI is installed.

Project creation supports an optional `Idempotency-Key` header. Exact retries return the stored
`201` response and `Idempotency-Replayed: true`; changed input returns
`idempotency_key_conflict`. Records expire after 24 hours and an expiry index supports a future
scheduled purge.

Run a conservative authenticated local smoke check without printing the token:

```bash
WORKOPS_ACCESS_TOKEN=... WORKOPS_WORKSPACE_ID=... ./scripts/load-smoke.sh
```

The script defaults to 20 sequential feature reads and labels its output as a local smoke result,
not a production benchmark.

Run the complete local backend scenario with `./scripts/demo.sh --start` or
`./scripts/demo.ps1 -Start`. Successful resource IDs and opaque versions are stored under the
ignored `.local/` directory; access tokens are not intentionally printed or persisted by the scripts.

## Release process

The release workflow accepts only `vMAJOR.MINOR.PATCH` tags at the current `master` head. A read-only
job restores locked dependencies, verifies formatting, builds, tests with the same coverage gate as
CI, audits NuGet packages, scans Git history, builds and scans the container, generates an SPDX JSON
SBOM, and packages a checksummed candidate. Only after it succeeds does the protected publication
job receive write permissions, load that exact candidate, publish the version and commit tags to
GHCR, record the immutable registry digest, and create release notes with the evidence attached.

Configure the `release` environment before publishing and add a required reviewer only when that
reviewer is genuinely independent. Treat tags as immutable and protect `v*` tags from update or
deletion. Repository-side controls that cannot be committed are listed in
[GitHub settings](github-settings.md).
