# Operations

## Current endpoints

- `/health/live` reports whether the process can serve requests and intentionally runs no dependency
  checks.
- `/health/ready` runs registered readiness checks. The foundation has no external dependencies, so
  this currently reports healthy when the application starts correctly.

## Local commands

```bash
dotnet run --project src/WorkOps.Api
docker compose config
docker compose up --build
docker compose down
```

## Planned diagnostics

Requests will receive a safe correlation identifier aligned with structured logs and distributed
trace context. OpenTelemetry will cover ASP.NET Core, outbound HTTP, EF Core, and messaging. Metrics
will avoid personal data and high-cardinality labels while covering request results, outbox backlog,
message processing, cache outcomes, and job duration.

A future runbook will trace one failed request from response correlation ID to logs, trace spans,
database/outbox state, retry history, and safe recovery. Replay operations will be authorized and
audited.
