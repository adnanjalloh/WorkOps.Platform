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
| Cross-workspace object access | Implemented for workspace, project, and work-item APIs | Verified context, default-deny filters, composite ownership constraints, non-disclosing denial, PostgreSQL and functional tests |
| Forged or malformed JWT | Implemented | Strict validation, provider boundary, missing-token and wrong-audience tests |
| Stolen valid JWT | Partial | Short lifetime is validated; revocation and deployed-provider operations remain external concerns |
| Privilege escalation | Partial | Central permission policies, contributor/viewer invitation limits, endpoint checks, viewer denial, and active-member assignment checks exist; administrator membership management and audit evidence remain planned |
| Lost concurrent update | Implemented | Opaque `xmin` token, expected-version updates, real PostgreSQL collision test, and safe `409 Conflict` |
| Replayed HTTP request | Planned | Scoped idempotency key plus canonical body hash and unique constraint |
| Duplicate or reordered message | Planned | Deterministic message ID, inbox/uniqueness record, retry tests |
| Malicious attachment | Planned | Size/type/signature allowlists, private storage, scanner port, download authorization |
| Secret leakage in logs or CI | Partial | Input rejection logs metadata only; Gitleaks and least-privilege workflows exist; broader redaction tests remain |
| Cross-workspace cache collision | Planned | Typed tenant-aware keys, invalidation/versioning, isolation tests |
| Mass assignment | Implemented for current contracts | Explicit request contracts omit tenant ownership and persistence fields; assignees must be active current-workspace members |
| Resource exhaustion | Partial | Request bodies and project pagination are bounded; rate limits and job retry ceilings remain |
| Compromised CI action | Implemented baseline | Full commit-SHA pins, minimal token permissions, dependency review |
| Over-privileged deployment identity | Planned | Workload identity, scoped roles, protected environments, auditable deploy job |

## Residual risk

Application-level query filters are backed by composite relational ownership constraints;
PostgreSQL row-level security is deferred and should be reconsidered as data sensitivity and scale
grow. The identity provider controls signing-key lifecycle and token revocation. CI, reviewable
increments, tests, and updated ADRs reduce but do not eliminate supply-chain, configuration, or
future-feature risk.
