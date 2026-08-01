# Threat model

## Status and method

This initial model defines the abuse cases that future vertical slices must address. Controls marked
"planned" are not implemented claims. The model will be updated with each trust boundary.

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

| Abuse case | Planned control and evidence |
|---|---|
| Cross-workspace object access | Verified workspace context, tenant-required queries, non-disclosing denial, functional tests |
| Forged, stolen, or malformed JWT | Strict validation, short lifetimes, provider boundary, negative token tests |
| Privilege escalation | Central permission policies, resource checks, assignment ceiling, audit tests |
| Replayed HTTP request | Scoped idempotency key plus canonical body hash and unique constraint |
| Duplicate or reordered message | Deterministic message ID, inbox/uniqueness record, retry tests |
| Malicious attachment | Size/type/signature allowlists, private storage, scanner port, download authorization |
| Secret leakage in logs or CI | Structured allowlisted fields, redaction tests, Gitleaks, least-privilege workflows |
| Cross-workspace cache collision | Typed tenant-aware keys, invalidation/versioning, isolation tests |
| Mass assignment | Explicit contracts and mapping; no mutable tenant, role, audit, or ownership fields |
| Resource exhaustion | Request limits, bounded pagination, rate limits, job retry ceilings |
| Compromised CI action | Full commit-SHA pins, minimal token permissions, dependency review |
| Over-privileged deployment identity | Workload identity, scoped roles, protected environments, auditable deploy job |

## Residual risk

The foundation does not yet process business data or authentication tokens. Its principal current
risks are supply-chain drift and future code failing to implement the planned boundaries. CI,
reviewable increments, tests, and updated ADRs reduce but do not eliminate those risks.
