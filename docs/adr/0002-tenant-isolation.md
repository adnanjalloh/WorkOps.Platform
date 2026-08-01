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
No context means no workspace or tenant-owned rows are visible. Each filtered EF type carries a
tenant-ID property annotation: `Workspace.Id` for the root and `WorkspaceId` for child entities.
Before saving, the database context resolves that metadata for every added, modified, or deleted
entry and checks current/original ownership. Root creation succeeds only in a disposable
provisioning scope for the generated ID; later root writes require a matching request/background
context. Application stores do not expose an unfiltered query surface.

All new request strings require a named sanitization profile or a documented skip reason. Tests use
real PostgreSQL to apply migrations and exercise the no-context and cross-workspace cases. Functional
tests exercise the complete JWT, middleware, authorization, and persistence path.

## Consequences

- Missing context fails closed without relying on each query author.
- Tenant-root and child writes fail closed on missing context, cross-workspace changes, and ID mutation.
- Membership resolution remains reviewable because filter bypass is isolated to one adapter.
- Background workers will need an explicit, validated workspace scope before tenant-owned access.
- Bulk operations and future administrative tooling cannot reuse tenant stores to bypass isolation.
- Non-disclosing denial limits workspace enumeration through current endpoints.

## Residual risk

Global query filters and the save-time write guard are application controls, not a database security
boundary. PostgreSQL row-level security is deferred and should be reconsidered if data sensitivity
or deployment risk grows. Raw SQL, direct bulk operations, new filter bypasses, background
processing, cache keys, messages, and file paths require separate tenant-bound review and tests.
