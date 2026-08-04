# Testing strategy

## Current evidence

The production-hardening milestone includes four test projects with 106 tests:

- Unit: 53 tests for workspace identifiers, sanitization malicious corpus, role permissions, project archiving,
  work-item state transitions and updates, version-token conversion, background tenant context,
  message validation, outbox lifecycle, deterministic retry policy, processor outcomes, feature
  quotas, filename/media-type sanitization, and attachment size/type/signature validation.
- Integration: 24 tests against real PostgreSQL migrations and default-deny query filters, including switching
  between two workspaces, a two-context optimistic-concurrency collision, concurrent outbox lease
  contention, durable publisher-confirmed routing through a real RabbitMQ container, tenant-aware
  cache isolation and invalidation through a real Redis container, concurrent quota reservations,
  tenant-separated local file paths, and missing/cross-workspace write rejection for insert, update,
  delete, and workspace-ID mutation paths.
- Functional: 21 tests using real PostgreSQL plus the ASP.NET Core host, locally signed test JWTs,
  exact OIDC-subject validation and preservation, token rejection,
  cross-workspace denial, permissions, suspension, inactive membership, malicious input, project
  lifecycle, invitation and assignment boundaries, labeled work-item updates and transitions,
  pagination and filters, archive behavior, stale-version conflicts, atomic audit/outbox evidence,
  duplicate delivery suppression, notification reads, audit authorization, replay protection,
  feature limit responses and plan changes, hostile upload rejection, exact attachment download,
  and cross-workspace attachment denial, correlation/trace response metadata, security headers,
  tested-log redaction, untrusted CORS denial, deterministic rate-limit responses, production HSTS
  and hidden OpenAPI, plus idempotent replay, changed-body denial, and scope isolation.
- Architecture: 8 tests for dependency direction, request sanitization-policy coverage,
  model-driven query-filter/write-resolver coverage, verification checkout policy, Docker-context
  security patterns, API persistence boundaries, and public-contract isolation.

These tests exercise the implemented identity, workspace, project, work-item, audit, outbox,
RabbitMQ, Redis, feature-limit, file-storage, and idempotent development-notification flow. The
functional suite replaces external message/file scanner behavior with deterministic test adapters,
while the integration suite exercises the real broker, cache, database, and local-storage adapters.
The tests check registration and HTTP behavior for observability/hardening; they do not validate a
specific collector backend, reverse-proxy configuration, or production capacity.

Local Bash and PowerShell demo runs reported the idempotent project replay, viewer `403`, stale
`409`, outsider `404`, asynchronous audit/notification evidence, and a non-duplicating repeat path.
The scheduled/manual `Full stack demo` workflow is configured to run the Bash scenario from a clean
host after non-installing bootstrap validation, screen its logs for bearer/JWT/credential markers
before displaying or uploading them, and retain synthetic log/JSON evidence only when that
fail-closed gate passes. Its UTC timestamps and elapsed seconds are dated observations, not an SLA.
The [public full-stack run] passed
at the reviewed commit. These scripts are reviewer tools, not substitutes for the automated suites.

Local verification on 2026-08-02 reported 90.3% line coverage and 48.9% branch coverage, with all 106
tests passing. Public [CI] and [CodeQL] runs also passed at the reviewed commit. CI merges collector
output, publishes HTML/Cobertura/Markdown evidence, and requires at least 70% lines and 35% branches.
These are regression floors rather than quality targets.

[public full-stack run]: https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727906844
[CI]: https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727069465
[CodeQL]: https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727069476

## Planned suites

- Additional message-transport tests for connection recovery and failed-queue operator workflows.
- Security regression tests for invalid tokens, privilege escalation, hostile uploads, oversized
  requests, safe logs/errors, CORS, and production OpenAPI behavior remain active and expand with
  each endpoint family.

Container-backed suites use supported providers rather than mocked database behavior. Time-dependent
tests use `TimeProvider`; tests do not depend on sleeps or local time.

## Commands

Validate the containerized reviewer path without installing tools or starting services:

```bash
./scripts/bootstrap.sh
```

CI runs the Bash and PowerShell bootstrap validators before building the container. The manual and
scheduled full-stack workflow then exercises the Bash path and complete synthetic scenario from a
clean hosted runner.

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --logger "trx" --collect:"XPlat Code Coverage"
```

To reproduce the CI coverage gate:

```bash
dotnet tool restore
dotnet test -c Release --no-build --maxcpucount:1 --collect:"XPlat Code Coverage" --results-directory artifacts/test-results -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[WorkOps.*Tests]*"
dotnet tool run reportgenerator -- "-reports:artifacts/test-results/**/coverage.cobertura.xml" "-targetdir:artifacts/coverage" "-assemblyfilters:+WorkOps.*;-WorkOps.*Tests;-* *" "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated.*;-System.Runtime.CompilerServices.*" "-reporttypes:Cobertura;TextSummary"
./scripts/check-coverage.sh artifacts/coverage/Cobertura.xml 70 35
```
