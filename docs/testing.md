# Testing strategy

## Current evidence

The project and work-item milestone includes four test projects with 40 tests:

- Unit: workspace identifiers, sanitization malicious corpus, role permissions, project archiving,
  work-item state transitions and updates, version-token conversion, and one-time context
  establishment.
- Integration: real PostgreSQL migrations and default-deny query filters, including switching
  between two workspaces, plus a real two-context optimistic-concurrency collision.
- Functional: real PostgreSQL plus the ASP.NET Core host, locally signed test JWTs, token rejection,
  cross-workspace denial, permissions, suspension, inactive membership, malicious input, project
  lifecycle, invitation and assignment boundaries, labeled work-item updates and transitions,
  pagination and filters, archive behavior, and stale-version conflicts.
- Architecture: dependency direction, request sanitization-policy coverage, and tenant-owned entity
  classification, plus API persistence-boundary and public-contract isolation checks.

These tests prove the implemented identity, workspace, project, and initial work-item flow. They do
not yet prove messaging, caching, file storage, observability, or production readiness.

## Planned suites

- Unit tests for feature limits, upload validation, backoff, and injected time.
- Additional PostgreSQL integration tests for outbox locks and idempotency.
- Redis and message-transport integration tests for tenant-safe keys and duplicate handling.
- Functional tests for the remaining golden scenario, outbox delivery, idempotency mismatch, and
  rate limiting.
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
