# Testing strategy

## Current evidence

The caching, limits, and secure-files milestone includes four test projects with 73 tests:

- Unit: workspace identifiers, sanitization malicious corpus, role permissions, project archiving,
  work-item state transitions and updates, version-token conversion, background tenant context,
  message validation, outbox lifecycle, deterministic retry policy, processor outcomes, feature
  quotas, filename/media-type sanitization, and attachment size/type/signature validation.
- Integration: real PostgreSQL migrations and default-deny query filters, including switching
  between two workspaces, a two-context optimistic-concurrency collision, concurrent outbox lease
  contention, durable publisher-confirmed routing through a real RabbitMQ container, tenant-aware
  cache isolation and invalidation through a real Redis container, concurrent quota reservations,
  and tenant-separated local file paths.
- Functional: real PostgreSQL plus the ASP.NET Core host, locally signed test JWTs, token rejection,
  cross-workspace denial, permissions, suspension, inactive membership, malicious input, project
  lifecycle, invitation and assignment boundaries, labeled work-item updates and transitions,
  pagination and filters, archive behavior, stale-version conflicts, atomic audit/outbox evidence,
  duplicate delivery suppression, notification reads, audit authorization, replay protection,
  feature limit responses and plan changes, hostile upload rejection, exact attachment download,
  and cross-workspace attachment denial.
- Architecture: dependency direction, request sanitization-policy coverage, and tenant-owned entity
  classification, plus API persistence-boundary and public-contract isolation checks.

These tests prove the implemented identity, workspace, project, work-item, audit, outbox, RabbitMQ,
Redis, feature-limit, file-storage, and idempotent development-notification flow. The functional
suite replaces external message/file scanner behavior with deterministic test adapters, while the
integration suite independently proves the real broker, cache, database, and local-storage adapters.
The tests do not yet prove full observability or production readiness.

## Planned suites

- Additional message-transport tests for connection recovery and failed-queue operator workflows.
- Functional tests for HTTP idempotency mismatch and rate limiting.
- Security regression tests for invalid tokens, privilege escalation, hostile uploads, oversized
  requests, safe logs/errors, CORS, and production OpenAPI behavior.

Container-backed suites use supported providers rather than mocked database behavior. Time-dependent
tests use `TimeProvider`; tests do not depend on sleeps or local time.

## Commands

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger "trx" --collect:"XPlat Code Coverage"
```
