# ADR 0002: Tenant isolation at the application persistence boundary

- Status: Accepted
- Date: 2026-08-01

## Context

Every workspace-owned read must be isolated even when a handler omits an explicit workspace
predicate. The caller-provided workspace identifier is not trustworthy by itself: it must be bound
to the validated token subject through an active membership. Denial must not reveal whether another
workspace exists.

## Decision

The API establishes workspace context in middleware after JWT validation. Workspace selection comes
from a typed route value or the `X-Workspace-Id` header. A dedicated access reader performs the only
deliberate unfiltered membership lookup and requires the validated `sub`, requested workspace, and
an active membership. Suspended workspaces return `403`; absent, inactive, and foreign memberships
return the same non-disclosing `404`.

Normal Entity Framework reads use global query filters over a nullable request-scoped workspace key.
No context means no workspace or membership rows are visible. Application stores do not expose an
unfiltered query surface. Role permissions are centralized and enforced by endpoint policies.

All new request strings require a named sanitization profile or a documented skip reason. Tests use
real PostgreSQL to apply migrations and prove the no-context and cross-workspace cases. Functional
tests exercise the complete JWT, middleware, authorization, and persistence path.

## Consequences

- Missing context fails closed without relying on each query author.
- Membership resolution remains reviewable because filter bypass is isolated to one adapter.
- Background workers will need an explicit, validated workspace scope before tenant-owned access.
- Bulk operations and future administrative tooling cannot reuse tenant stores to bypass isolation.
- Non-disclosing denial limits workspace enumeration through current endpoints.

## Residual risk

Global query filters are an application control, not a database security boundary. PostgreSQL
row-level security is deferred while the data model contains only identity and workspace metadata.
It must be reconsidered before sensitive project, work-item, audit, or attachment data is released.
Raw SQL, new filter bypasses, background processing, cache keys, messages, and file paths require
separate tenant-bound review and tests.
