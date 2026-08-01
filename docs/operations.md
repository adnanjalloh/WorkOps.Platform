# Operations

## Current endpoints

- `/health/live` reports whether the process can serve requests and intentionally runs no dependency
  checks.
- `/health/ready` verifies PostgreSQL connectivity and, when messaging is enabled, RabbitMQ
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
and starts the notification consumer.

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
