# Reviewer guide

WorkOps.Platform is a portfolio release candidate for reviewing backend architecture, security boundaries, reliability, testing, and delivery. It is not a hosted production service.

Choose the path that matches the time and question you have.

## Two-minute tour

1. Read the [README status and proof summary](../README.md#portfolio-release-candidate).
2. Scan the [runtime architecture](../README.md#architecture) and the [golden scenario](../README.md#golden-scenario).
3. Open the [tenant context middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs) to see how authenticated membership becomes workspace context.
4. Open the [golden-flow functional test](../tests/WorkOps.FunctionalTests/TenantIdentityEndpointTests.cs) for executable authorization, concurrency, audit, and messaging evidence.
5. Check the [evidence index](evidence.md) for dated claims and limitations.

Expected proof point: the repository makes tenant isolation, stale-write rejection, idempotent commands, and outbox-backed notification delivery directly traceable from public claims to code and tests.

## Ten-minute backend review

### 1. Boundaries and use cases

- [Architecture overview](architecture.md)
- [Domain project](../src/WorkOps.Domain)
- [Application services](../src/WorkOps.Application)
- [Public contracts](../src/WorkOps.Contracts)
- [Infrastructure adapters](../src/WorkOps.Infrastructure)
- [API adapter and composition root](../src/WorkOps.Api)

### 2. Tenant isolation and authorization

- [Tenant-isolation ADR](adr/0002-tenant-isolation.md)
- [Workspace context middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs)
- [Permission authorization handler](../src/WorkOps.Api/Authorization/PermissionAuthorizationHandler.cs)
- [Tenant query/write boundary tests](../tests/WorkOps.IntegrationTests/TenantQueryFilterTests.cs)

Review for default-deny behavior: an authenticated identity is insufficient without an active membership, tenant-filtered entities have save-time guards, and cross-workspace reads do not disclose resource existence.

### 3. Consistency and reliable delivery

- [Work-item service](../src/WorkOps.Application/WorkItems/WorkItemService.cs)
- [Outbox ADR](adr/0003-outbox-delivery.md)
- [Outbox processor](../src/WorkOps.Application/Messaging/OutboxProcessor.cs)
- [Outbox worker](../src/WorkOps.Infrastructure/Messaging/OutboxWorker.cs)
- [Notification handler](../src/WorkOps.Application/Messaging/NotificationMessageHandler.cs)

Review the transaction boundary, lease/retry lifecycle, publisher confirmation, manual acknowledgment, and inbox-backed duplicate suppression.

### 4. Delivery evidence

- [Testing strategy](testing.md)
- [Demo guide](demo.md)
- [CI workflow](../.github/workflows/ci.yml)
- [Release workflow](../.github/workflows/release.yaml)
- [Dockerfile](../Dockerfile)

## Security review

Start with the [threat model](threat-model.md), then use the [security controls](security.md) and [OWASP ASVS evidence map](asvs-map.md) as navigation aids. The map is not a certification.

Focus on:

- JWT issuer, audience, signature, lifetime, algorithm, and subject validation;
- active membership and permission checks;
- query filtering plus write-boundary enforcement;
- request bounds, sanitization profiles, and safe diagnostics;
- attachment validation, scanning boundary, opaque storage names, and private paths;
- secret handling, workflow pinning, dependency review, CodeQL, and image scanning gates.

Known boundary: the local identity realm, development scanner, and temporary local file storage are demonstration adapters. They must be replaced and operated appropriately before a real deployment.

## Operations and reliability review

Read [operations](operations.md) for health checks, message recovery, cache and attachment settings, HTTP controls, observability, and release operations. Then inspect:

- [production configuration validation](../src/WorkOps.Api/Operations/ProductionConfiguration.cs);
- [observability registration](../src/WorkOps.Api/Observability/ObservabilityExtensions.cs);
- [outbox backlog monitor](../src/WorkOps.Infrastructure/Messaging/OutboxBacklogMonitor.cs);
- [attachment reconciliation script](../scripts/reconcile-attachments.sh).

The repository includes instrumentation and safe local operational paths; it does not claim a hosted collector, production dashboards, backup validation, or production service-level results.

## Run the proof locally

Prerequisites: Docker Desktop, `curl`, and `jq` on macOS/Linux, or PowerShell 7 on Windows.

```bash
./scripts/demo.sh --start
```

The scenario uses synthetic users and checks role denial, idempotent replay, concurrency conflict, non-disclosing cross-workspace access, audit evidence, and outbox notification delivery. Stop the stack with `docker compose down`.

## Evidence rules

- Treat every count, percentage, scan result, and workflow result as dated and commit-specific.
- Prefer generated output and linked tests over configuration-only claims.
- Do not infer production deployment, production users, penetration testing, cloud deployment, or scale from this repository.
- See the [portfolio case study](portfolio-case-study.md) for design tradeoffs and the [evidence index](evidence.md) for current public proof.
