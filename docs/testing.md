# Testing strategy

## Current evidence

The tenant and identity milestone includes four test projects with 26 tests:

- Unit: workspace identifiers, sanitization malicious corpus, role permissions, and one-time context
  establishment.
- Integration: real PostgreSQL migrations and default-deny query filters, including switching
  between two workspaces.
- Functional: real PostgreSQL plus the ASP.NET Core host, locally signed test JWTs, token rejection,
  cross-workspace denial, permissions, suspension, inactive membership, and malicious input.
- Architecture: dependency direction, request sanitization-policy coverage, and tenant-owned entity
  classification.

These tests prove the implemented identity and workspace boundary. They do not yet prove the future
work-item flow, messaging, caching, file storage, observability, or production readiness.

## Planned suites

- Unit tests for work-item state transitions, feature limits, upload validation, backoff, and
  injected time.
- Additional PostgreSQL integration tests for concurrency, outbox locks, and idempotency.
- Redis and message-transport integration tests for tenant-safe keys and duplicate handling.
- Functional tests for the golden scenario, realistic tokens, Problem Details, denial paths,
  concurrency conflicts, idempotency mismatch, and rate limiting.
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
