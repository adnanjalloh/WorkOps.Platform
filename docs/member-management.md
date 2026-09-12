# Workspace member management

Workspace membership supports listing, direct invitation of an existing identity subject, role
changes, and deactivation. This is workspace authorization, not identity-provider account
administration or an email invitation/acceptance workflow. See [issue #72](https://github.com/adnanjalloh/WorkOps.Platform/issues/72).

## Authority

| Caller | Change role | Deactivate |
| --- | --- | --- |
| Owner | Any active member to any named role, including Owner | Any member, subject to last-owner protection |
| Administrator | Only Contributor/Viewer members, and only between those two roles | Only Contributor/Viewer members |
| ProjectContributor or Viewer | Denied | Denied |
| Inactive member or outsider | Non-disclosing workspace `404` | Non-disclosing workspace `404` |

The actual contributor role name is `ProjectContributor`. Administrators cannot change their own
privileged membership, modify another administrator/owner, or grant either privileged role. Owners
may demote or deactivate themselves only if another active owner remains. Invitations still allow
only `ProjectContributor` and `Viewer`; an owner must explicitly promote an invited member.

## HTTP contract

Authenticate with a bearer token. The workspace comes from the **route**, not `X-Workspace-Id`.
Obtain the member's `userId` and opaque `version` from
`GET /api/v1/workspaces/{workspaceId}/members`; invitation and mutation responses include them too.
Never derive a version from a timestamp or reuse a version from a different member.

```http
PATCH /api/v1/workspaces/{workspaceId}/members/{userId}/role
Content-Type: application/json

{"role":"ProjectContributor","expectedVersion":"000001AB"}
```

```http
POST /api/v1/workspaces/{workspaceId}/members/{userId}/deactivation
Content-Type: application/json

{"expectedVersion":"000001AC"}
```

Versions above are illustrative; use the value returned by the API. Role names are case-insensitive
after identifier sanitization, but numeric enum values are rejected. Versions are exactly eight
ASCII hexadecimal characters, accepted without trimming or Unicode normalization. Empty member
IDs, missing versions, and invalid roles/versions return `422`.

Both mutations return `200` with `userId`, `displayName`, `role`, `isActive`, and `version`.
A changed membership has a new version. A same-role change or already-inactive deactivation using
the **current** version is a no-op: no new audit event or version. A stale version always returns
`409 concurrency_conflict`, including retries of a successful change. Read the latest state and
reassess intent before retrying; do not blindly overwrite it.

Missing/foreign target members return the same empty `404` response. Insufficient management
authority returns `403`; a last-owner violation returns `409 last_workspace_owner`. A role change
on an inactive membership returns `409 inactive_workspace_membership`. Privileged-target denials
take precedence over version/state conflict details for administrators. Existing middleware still
rejects unauthenticated requests and suspended-workspace access.

## Concurrency and revocation

Every membership mutation, including invitation, acquires the same tenant-scoped PostgreSQL
workspace-row lock inside its transaction. After the lock, the service reloads the caller's active
membership/role and workspace status before inspecting or mutating the target. The last-owner check
therefore cannot race another lifecycle write in the same workspace. Membership `xmin` additionally
enforces optimistic concurrency at persistence. Mutation and audit commit together; errors and
cancellation roll both back. See [ADR 0007](adr/0007-member-lifecycle-concurrency.md).

Existing valid tokens do not retain old workspace privileges: subsequent requests resolve active
membership and permissions from PostgreSQL, not token role claims or Redis. Waiting membership
writes recheck authority after acquiring their lock. This does **not** abort every already-running
business request or revoke the OIDC token globally; previously authorized work-item requests and
already-queued notifications may still complete. Other workspace memberships are unaffected.

Deactivation preserves the membership row, audit history, existing assignments, files, and other
business data. New assignment to an inactive member is rejected. Existing work items can be
reassigned to an active member or cleared through the normal update contract. Deactivation does
not cancel already-enqueued notifications, erase user data, or free identity subjects for
re-invitation. Reactivation, account deletion, email delivery, and provider-side revocation are
outside this change.

Audit events are `member.role_changed` (previous/current role) and `member.deactivated` (role), with
the existing generated entity/actor/workspace/correlation identifiers. Submitted display names,
identity subjects, tokens, and version strings are not added to their metadata.

## Compatibility and evidence

`MembershipConcurrency` updates the EF model to map PostgreSQL's existing `xmin` system column;
Npgsql emits no physical column add/drop for this mapping. The migration history advances, but
existing membership data and system versions are preserved. Apply migrations through the existing
operational path. Downgrading this migration does not delete the system column or business data.
The additive JSON `version` field is ignored by tolerant older clients; strict response-schema
clients must update their schema. Older servers do not expose the new mutation routes.

The [HTTP tests](../tests/WorkOps.FunctionalTests/WorkspaceMemberEndpointTests.cs) exercise authority,
version/no-op semantics, revocation with unchanged tokens, cross-workspace isolation, input rejection,
and audit fields. The [database tests](../tests/WorkOps.IntegrationTests/MembershipConcurrencyTests.cs)
exercise owner races, conflicting edits, stale authorization, tenant-scoped locking, cancellation,
post-save rollback, `xmin` enforcement, and upgrade/downgrade on an isolated PostgreSQL database.
These are regression checks, not a production load test or independent security certification.
