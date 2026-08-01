# Demo

## Current demo

The current milestone exposes two operational endpoints:

```text
GET /health/live
GET /health/ready
```

The authenticated workspace boundary also exposes:

```text
GET  /api/v1/me/
GET  /api/v1/me/capabilities
POST /api/v1/workspaces/
GET  /api/v1/workspaces/{workspaceId}
GET  /api/v1/workspaces/{workspaceId}/members
POST /api/v1/workspaces/{workspaceId}/invitations
POST /api/v1/projects/
GET  /api/v1/projects/
GET  /api/v1/projects/{projectId}
POST /api/v1/projects/{projectId}/archive
POST /api/v1/projects/{projectId}/work-items
GET  /api/v1/work-items/{workItemId}
PATCH /api/v1/work-items/{workItemId}
POST /api/v1/work-items/{workItemId}/transitions
```

Run the Compose stack to use the local identity provider and PostgreSQL. The automated functional
suite executes the initial golden flow with owner, contributor, viewer, and outsider identities. It
verifies assignment, labels, transitions, tenant and role boundaries, archive behavior, pagination,
filtering, and stale-version handling. No hosted demo is claimed yet.

## Planned golden scenario

A deterministic script will expose the already-tested project and work-item flow, then show the
planned audit and notification result. The existing automated scenario proves that a caller from a
second workspace cannot infer the work item's existence and that a stale version returns `409
Conflict`. Audit and notification delivery are the next milestone.

The final script will be idempotent, print no tokens or credentials, and include Bash and PowerShell
entry points.
