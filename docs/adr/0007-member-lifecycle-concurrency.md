# ADR 0007: Serialize workspace membership changes

- Status: Accepted for the member-management implementation
- Date: 2026-09-12
- Scope: [Issue #72](https://github.com/adnanjalloh/WorkOps.Platform/issues/72)

## Context

An individual row's optimistic version cannot prevent write skew across two different owners:
both could see another owner, then demote/deactivate separate rows and leave the workspace without
an active owner. Middleware authorization also becomes stale if a request waits while another
request removes its caller's management authority.

## Decision

Use a parameterized `SELECT ... WHERE "Id" = @workspaceId FOR NO KEY UPDATE` on the workspace row,
retaining the EF tenant filter. Acquire it at the start of the membership transaction, before any
membership read/write. Check fresh caller activity/role and workspace status after acquisition.
All invitation, role-change, and deactivation paths share this ordering. Provisioning still creates
the first owner atomically in its existing private new-workspace transaction.

Hold the lock through target checks, last-owner validation, mutation, audit save, and transaction
commit. `FOR NO KEY UPDATE` serializes these operations without blocking unrelated workspace
foreign-key checks. It does not lock other workspaces. Command timeouts and caller cancellation
remain in effect. Future membership writers must use this same transaction/lock protocol; this
is not a database trigger that protects arbitrary administrator SQL.

Expose a membership version using the established eight-character hexadecimal convention. Map a
`uint` property to PostgreSQL `xmin` with `IsRowVersion()`. Check the client's expected version and
keep EF's update predicate so stale tracked writes are also rejected. Do not normalize opaque
tokens. No-op mutations require the current version and do not manufacture audit activity.

Role authority is evaluated from the fresh actor and target, not enum numeric ordering. Owners may
grant any named role; administrators may only manage ordinary contributors/viewers. All valid
operations preserve an active owner. Deactivation retains identity and audit/assignment references.

## Alternatives and consequences

- A count followed by ordinary row updates permits cross-row write skew.
- Locking only the target cannot protect two different owners or a concurrently revoked actor.
- Serializable isolation could reject write skew but adds serialization failure/retry handling to
  this narrowly scoped operation. A common workspace row gives an explicit, testable ordering.
- A distributed cache lock would introduce an unnecessary authorization dependency and failure mode.

Membership writes within one workspace are serialized; ordinary reads and other workspaces are
not. This is a deliberate bounded business-workflow tradeoff, not a throughput claim. Normal
requests resolve permissions on each request; only membership writes additionally recheck after
their lock. In-flight unrelated business requests and queued messages are not globally cancelled.

## Verification and references

The [contract and test map](../member-management.md) covers HTTP denials, version conflicts, two-owner
races, stale caller authority (including invitation), post-save transactional rollback, lock
cancellation/isolation, and migration upgrade/rollback.

- [PostgreSQL row locks](https://www.postgresql.org/docs/18/explicit-locking.html#LOCKING-ROWS)
- [Npgsql concurrency tokens and xmin mapping](https://www.npgsql.org/efcore/modeling/concurrency.html)
