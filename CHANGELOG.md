# Changelog

All notable changes to this project will be documented here. The project follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and will use semantic versioning after the
first release.

## [Unreleased]

### Added

- Tenant-scoped work-item listing with bounded, stable pagination and composable project, status,
  assignee, and literal title filters; supporting indexes, API examples, and HTTP boundary tests.
- Messaging recovery tests for cancellation, exhausted retries, abandoned leases, and notification
  deduplication after an injected completion-write failure.

### Changed

- Align SDK 10.0.401 across local metadata, Docker, CI, CodeQL, and release verification; update
  runtime, ASP.NET Core/EF/Extensions packages, and dotnet-ef together to 10.0.12.
- Update Microsoft.OpenApi to 2.12.0 to satisfy ASP.NET Core OpenAPI's new minimum, and
  Microsoft.NET.Test.Sdk to 18.10.0; regenerate the solution dependency locks.
- Group related Microsoft platform/OpenAPI updates and .NET container images for maintenance.
- Refresh the two-minute interview guide and both demo scripts to demonstrate combined filters
  and isolated result counts; exercise Bash/PowerShell fresh and replay paths in the hosted scenario.
- Update Microsoft.NET.Test.Sdk to 18.9.0 and the MSTest adapter/framework together to 4.4.0.
- Refresh pinned release actions to anchore/sbom-action 0.24.2 and softprops/action-gh-release 3.0.3.
- Upgrade OpenTelemetry packages together to 1.18.0 and StackExchange.Redis to 3.1.31.
- Update CodeQL initialization and analysis together to the pinned 4.37.9 release.
- Group related OpenTelemetry, MSTest, and CodeQL Dependabot updates and document solution-wide
  lockfile refresh, required checks, and superseded-PR handling.
- Document OpenTelemetry 1.18.0's 64 MiB default OTLP request limit and 4 MiB response limit.

### Fixed

- Clear an uncommitted outbox completion timestamp when a failed completion save returns the message
  to pending or failed state, keeping retry status and diagnostics consistent.
- Regenerate downstream NuGet lockfiles for centrally pinned dependencies so locked restore includes
  the updated API, infrastructure, and test-project dependency graphs.

## [0.1.0] - 2026-08-04

### Added

- Approval-gated release provenance and SPDX SBOM attestations bound to the immutable GHCR digest,
  with prepared `v0.1.0` notes and consumer verification commands.
- Exact, case-sensitive, printable-ASCII OIDC subject validation and storage with full-host boundary tests.
- Save-time tenant ownership enforcement for every filtered type, including the `Workspace` root.
- Model-driven query-filter/write-resolver coverage plus cross-workspace root and child write tests.
- Deterministic visual asset generation with source, checksums, and licensing documentation.
- Scheduled/manual full-Compose golden-scenario verification with fail-closed evidence screening.
- Exact business-constraint race mapping and atomic identity/workspace provisioning.
- Corrupt-cache recovery and attachment storage integrity/reconciliation handling.
- Trusted-proxy forwarding, release-derived telemetry versions, and explicit OTLP transport policy.
- Sanitized outbox publish-failure diagnostics plus restricted worker exception diagnostics.
- Verification workflows with non-persisted checkout credentials and a regression test.
- Security-sensitive `.gitignore`/`.dockerignore` policy alignment.
- Bounded idempotency-record retention with cross-tenant maintenance isolation and metrics.
- Clean-room .NET 10 modular-monolith foundation.
- Health endpoints and initial functional test.
- Unit, integration-smoke, functional, and architecture test projects.
- Pinned container baseline and GitHub security/delivery automation.
- Initial architecture, security, testing, demo, operations, and threat-model documentation.
- PostgreSQL workspace, user, membership, and migration model.
- Strict JWT authentication and role-based permission policies.
- Verified request-scoped workspace context with default-deny tenant query filters.
- Sanitization profiles with automated request-surface coverage.
- Container-backed tenant-isolation and HTTP security regression tests.
- Tenant-safe project creation, lookup, filtering, pagination, and archiving.
- Contributor and viewer invitations with role and membership boundaries.
- Assigned, labeled work-item creation and updates with an explicit transition state machine.
- Opaque PostgreSQL `xmin` concurrency tokens with stale writes returned as `409 Conflict`.
- Functional coverage for the initial project and work-item golden scenario.
- Safe tenant-scoped audit history with bounded filtering and administrator authorization.
- Atomic work-item transition, audit-event, and outbox persistence.
- Leased outbox processing with bounded deterministic backoff and recoverable failure state.
- Publisher-confirmed RabbitMQ routing, explicit consumer acknowledgments, and a failed-message queue.
- Tenant-scoped inbox deduplication and a development notification feed.
- Protected, audited replay for failed outbox messages.
- Tenant-aware Redis feature caching with expiry, invalidation, stampede protection, and database
  fallback.
- `Starter` and `Team` workspace plans with an optimistic-concurrency protected active-project quota.
- Secure work-item attachment upload and download with bounded reads, filename/media/signature
  validation, a fail-closed scanner port, opaque tenant-separated storage, and SHA-256 metadata.
- PostgreSQL, Redis, storage, and HTTP regression tests for quota races, cache/file isolation,
  malicious uploads, oversized files, and non-disclosing cross-workspace downloads.
- Server-generated correlation/trace diagnostics, structured Serilog JSON output, and OpenTelemetry
  instrumentation with optional OTLP export.
- Deny-by-default CORS, user/IP rate limiting, HTTP security headers, header bounds, production
  HSTS/HTTPS rules, and Development-only OpenAPI JSON.
- Tenant/user/method/route scoped project-create idempotency with canonical request hashing,
  persisted successful responses, expiry, replay, mismatch denial, and a database race boundary.
- Low-cardinality outbox duration/backlog metrics and a conservative local load-smoke script.
- Merged coverage reporting with enforced 70% line and 35% branch floors.
- High/critical container vulnerability scanning in continuous integration and release jobs.
- Reduced container build context that excludes local build, test, and coverage artifacts.
- Full-commit action pins updated to current maintained major versions.
- Tag-gated releases that verify and checksum the candidate in a read-only job, then publish version
  and commit-addressed GHCR tags from a separate protected job with immutable digest evidence.
- OWASP ASVS 5.0 area mapping and an explicit repository-settings hardening checklist.
- Synthetic Keycloak demo identities with a CLI-to-API audience mapper and private-network
  backchannel discovery.
- Runnable Bash and PowerShell golden-scenario clients plus a named-request HTTP collection.
- Recruiter-focused README, implementation sequence, terminal walkthrough, repository metadata,
  and a vendor-logo-free social preview asset.
- StackExchange.Redis 3.1 with the current stable client improvements.
- MIT license for the independently written portfolio source.
