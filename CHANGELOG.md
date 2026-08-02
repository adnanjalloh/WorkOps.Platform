# Changelog

All notable changes to this project will be documented here. The project follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and will use semantic versioning after the
first release.

## [Unreleased]

### Added

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
