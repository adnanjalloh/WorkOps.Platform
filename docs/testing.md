# Testing strategy

## Current evidence

The foundation includes four test projects with a deliberately small evidence set:

- Unit: server-generated workspace identifiers are non-empty and unique.
- Integration smoke: the Infrastructure assembly loads with the expected identity.
- Functional: an in-memory ASP.NET Core host returns healthy from `/health/live`.
- Architecture: Domain has no project dependencies and Application cannot reference API or
  Infrastructure.

These tests prove only the foundation. They do not yet prove persistence, tenant isolation,
authorization, messaging, caching, file storage, or production readiness.

## Planned suites

- Unit tests for state transitions, permission calculation, feature limits, upload validation,
  backoff, and injected time.
- PostgreSQL integration tests for mappings, migrations, tenant filters, concurrency, outbox locks,
  and idempotency.
- Redis and message-transport integration tests for tenant-safe keys and duplicate handling.
- Functional tests for the golden scenario, realistic tokens, Problem Details, denial paths,
  concurrency conflicts, idempotency mismatch, and rate limiting.
- Security regression tests for invalid tokens, privilege escalation, hostile uploads, oversized
  requests, safe logs/errors, CORS, and production OpenAPI behavior.

Container-backed suites will use real supported providers rather than mocked database behavior.
Time-dependent tests will use `TimeProvider`; tests will not depend on sleeps or local time.

## Commands

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger "trx" --collect:"XPlat Code Coverage"
```
