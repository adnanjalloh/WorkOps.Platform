# ADR 0006: HTTP idempotency

- Status: Accepted for project creation
- Date: 2026-08-01

## Context

A client can lose a successful response and retry a project-create request. Processing both attempts
would create duplicate projects and consume two plan slots. Reusing a key with different input must
not return an unrelated cached response.

## Decision

`POST /api/v1/projects/` optionally accepts a sanitized `Idempotency-Key`. Records are scoped by the
established workspace, authenticated user, method, canonical route, and key. The stored SHA-256 hash
covers a length-prefixed canonical form of the sanitized name and project key.

The project, audit event, quota update, and successful `201` response record commit in one PostgreSQL
transaction. An exact retry returns the stored response and marks it with
`Idempotency-Replayed: true`. A different request hash returns `409 idempotency_key_conflict`. The
composite primary key prevents two first writers from both committing; the losing race receives a
safe retryable conflict. Records expire after 24 hours and an expired record is replaced when its
scoped key is reused.

Authorization failures and validation errors are never cached. Idempotency does not extend or cache
permissions: every replay request must still authenticate, establish workspace membership, and pass
the project-write policy before the stored response is read.

## Consequences

- The first release supports idempotency only for project creation; other retriable writes must opt
  in deliberately with their own canonical request definition.
- Successful response JSON is tenant-owned application data and follows the same database access
  controls as the project.
- An expiry index supports scheduled deletion, but the portfolio release does not run a purge worker.
- The response represents the original creation result, even if the project is later changed.

## Evidence

Functional tests check exact replay, one project-slot consumption, changed-body rejection, separate
tenant/user scopes, one persisted record per scope, and safe Problem Details.
