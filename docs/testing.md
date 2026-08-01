# Testing strategy

## Current evidence

The production-hardening milestone includes four test projects with 88 tests:

- Unit: workspace identifiers, sanitization malicious corpus, role permissions, project archiving,
  work-item state transitions and updates, version-token conversion, background tenant context,
  message validation, outbox lifecycle, deterministic retry policy, processor outcomes, feature
  quotas, filename/media-type sanitization, and attachment size/type/signature validation.
- Integration: 13 tests against real PostgreSQL migrations and default-deny query filters, including switching
  between two workspaces, a two-context optimistic-concurrency collision, concurrent outbox lease
  contention, durable publisher-confirmed routing through a real RabbitMQ container, tenant-aware
  cache isolation and invalidation through a real Redis container, concurrent quota reservations,
  tenant-separated local file paths, and missing/cross-workspace write rejection for insert, update,
  delete, and workspace-ID mutation paths.
- Functional: 18 tests using real PostgreSQL plus the ASP.NET Core host, locally signed test JWTs,
  exact OIDC-subject validation and preservation, token rejection,
  cross-workspace denial, permissions, suspension, inactive membership, malicious input, project
  lifecycle, invitation and assignment boundaries, labeled work-item updates and transitions,
  pagination and filters, archive behavior, stale-version conflicts, atomic audit/outbox evidence,
  duplicate delivery suppression, notification reads, audit authorization, replay protection,
  feature limit responses and plan changes, hostile upload rejection, exact attachment download,
  and cross-workspace attachment denial, correlation/trace response metadata, security headers,
  tested-log redaction, untrusted CORS denial, deterministic rate-limit responses, production HSTS
  and hidden OpenAPI, plus idempotent replay, changed-body denial, and scope isolation.
- Architecture: 6 tests for dependency direction, request sanitization-policy coverage,
  model-driven query-filter coverage for mapped tenant-owned entities, API persistence boundaries,
  and public-contract isolation.

These tests exercise the implemented identity, workspace, project, work-item, audit, outbox,
RabbitMQ, Redis, feature-limit, file-storage, and idempotent development-notification flow. The
functional suite replaces external message/file scanner behavior with deterministic test adapters,
while the integration suite exercises the real broker, cache, database, and local-storage adapters.
The tests check registration and HTTP behavior for observability/hardening; they do not validate a
specific collector backend, reverse-proxy configuration, or production capacity.

Local Bash and PowerShell demo runs reported the idempotent project replay, viewer `403`, stale
`409`, outsider `404`, asynchronous audit/notification evidence, and a non-duplicating repeat path.
The scheduled/manual `Full stack demo` workflow is configured to run the Bash scenario from a clean
host and retain its sanitized log and JSON summary; hosted evidence remains pending the first push.
These scripts are reviewer tools, not substitutes for the automated suites.

Local verification on 2026-08-01 reported 90.7% line coverage and 49.7% branch coverage, with all 88
tests passing. Hosted GitHub Actions evidence is pending the private first push. CI merges collector
output, publishes HTML/Cobertura/Markdown evidence, and requires at least 70% lines and 35% branches.
These are regression floors rather than quality targets.

## Planned suites

- Additional message-transport tests for connection recovery and failed-queue operator workflows.
- Security regression tests for invalid tokens, privilege escalation, hostile uploads, oversized
  requests, safe logs/errors, CORS, and production OpenAPI behavior remain active and expand with
  each endpoint family.

Container-backed suites use supported providers rather than mocked database behavior. Time-dependent
tests use `TimeProvider`; tests do not depend on sleeps or local time.

## Commands

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger "trx" --collect:"XPlat Code Coverage"
```

To reproduce the CI coverage gate:

```bash
dotnet tool restore
dotnet test -c Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[WorkOps.*Tests]*"
dotnet tool run reportgenerator -- "-reports:artifacts/test-results/**/coverage.cobertura.xml" "-targetdir:artifacts/coverage" "-assemblyfilters:+WorkOps.*;-WorkOps.*Tests;-* *" "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated.*;-System.Runtime.CompilerServices.*" "-reporttypes:Cobertura;TextSummary"
./scripts/check-coverage.sh artifacts/coverage/Cobertura.xml 70 35
```
