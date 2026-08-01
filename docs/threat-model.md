# Threat model

## Status and method

This model defines abuse cases for current and future vertical slices. Status is explicit so planned
controls are not mistaken for implemented claims. The model is updated with each trust boundary.

## Assets

- workspace-owned projects, work items, comments, audit events, and attachments;
- membership, role, permission, and feature-entitlement state;
- authentication tokens and provider configuration;
- message, idempotency, cache, and background-job state;
- source, build provenance, release artifacts, logs, traces, and configuration.

## Actors and trust boundaries

Actors include unauthenticated callers, workspace members, workspace administrators, maintainers,
background workers, and compromised external dependencies. Trust boundaries exist at HTTP, the
identity provider, database, cache, message transport, file storage, observability pipeline, CI,
and deployment environment.

## Required abuse-case coverage

| Abuse case | Status | Control and evidence |
|---|---|---|
| Cross-workspace object access | Implemented for workspace, project, work-item, audit, and notification APIs | Validated context, default-deny filters, metadata-driven root/child write guard, composite ownership constraints, non-disclosing denial, PostgreSQL and functional tests |
| Forged or malformed JWT | Implemented | Strict validation, printable-ASCII subject profile, control-character rejection, provider boundary, missing-token and wrong-audience tests |
| Stolen valid JWT | Partial | Short lifetime is validated; revocation and deployed-provider operations remain external concerns |
| Privilege escalation | Partial | Central permission policies, contributor/viewer invitation limits, endpoint checks, viewer audit denial, active-member assignment checks, and administrative audit evidence exist; broader membership management remains planned |
| Lost concurrent update | Implemented | Opaque `xmin` token, expected-version updates, real PostgreSQL collision test, and safe `409 Conflict` |
| Replayed HTTP request | Implemented for project creation | Tenant/user/method/route/key scope, canonical sanitized-body hash, persisted `201` response, 24-hour expiry, primary key race boundary, exact-replay and mismatch tests |
| Duplicate or reordered message | Partial | Stable message ID, tenant-scoped inbox and notification uniqueness, explicit acknowledgments, bounded retries, and duplicate tests exist; a business sequence guard for independently reordered events is not implemented |
| Compromised message transport | Partial | Internal envelopes are validated, payloads omit submitted content, invalid messages enter a failed queue, and handlers establish tenant context; broker TLS and production credentials remain deployment concerns |
| Malicious attachment | Implemented baseline | 512 KiB bound, filename/media/signature allowlists, strict text decoding, fail-closed scanner port, opaque private storage, authorized download, and hostile-upload tests; production antivirus remains required |
| Secret leakage in logs or CI | Partial | Input rejection logs metadata only; outbox diagnostics are sanitized, demo evidence is screened fail-closed, and verification checkouts do not persist Git credentials; hosted inspection remains pending |
| Cross-workspace cache collision | Implemented | Tenant-derived Redis keys, short expiry, explicit invalidation, PostgreSQL fallback, real Redis isolation tests |
| Mass assignment | Implemented for current contracts | Explicit request contracts omit tenant ownership and persistence fields; assignees must be active current-workspace members |
| Resource exhaustion | Implemented baseline | Request bodies/headers and pagination are bounded; jobs have leases, prefetch, and retry ceilings; user/IP fixed-window rate limits return safe `429`; capacity/load tuning remains deployment-specific |
| Compromised CI action | Implemented baseline | Current full commit-SHA pins, minimal default permissions, dependency review, CodeQL, Gitleaks, and Dependabot |
| Vulnerable or substituted release image | Implemented baseline | Read-only release verification, high/critical image scan, checksummed candidate handoff, version and commit tags, immutable digest evidence, and SPDX JSON SBOM; signing and provenance attestation remain planned |
| Over-privileged deployment identity | Planned | Workload identity, scoped roles, protected environments, auditable deploy job |

## Residual risk

Application-level query filters and the save-time tenant write guard are backed by composite
relational ownership constraints;
PostgreSQL row-level security is deferred and should be reconsidered as data sensitivity and scale
grow. The identity provider controls signing-key lifecycle and token revocation. CI, reviewable
increments, tests, and updated ADRs reduce but do not eliminate supply-chain, configuration, or
future-feature risk.
