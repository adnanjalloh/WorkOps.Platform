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

The current code emits low-cardinality `workops.outbox.results` and
`workops.notifications.results` counters. Export configuration and backlog gauges arrive with the
broader observability milestone.

## Planned diagnostics

Audit rows and messages already carry the server-generated request correlation identifier. A later
middleware and OpenTelemetry milestone will expose a deliberate response correlation contract and
cover ASP.NET Core, outbound HTTP, EF Core, and messaging with traces. Metrics will continue to
avoid personal data and high-cardinality labels.

A future runbook will trace one failed request from response correlation ID to logs, trace spans,
database/outbox state, retry history, and safe recovery.
