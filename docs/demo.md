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
GET  /api/v1/audit-events
GET  /api/v1/notifications
POST /api/v1/operations/outbox/{messageId}/replay
GET  /api/v1/features
PUT  /api/v1/workspaces/{workspaceId}/plan
POST /api/v1/work-items/{workItemId}/attachments
GET  /api/v1/attachments/{attachmentId}
```

Run the Compose stack to use the local identity provider, PostgreSQL, Redis, and RabbitMQ. The automated
suite executes the backend golden flow with owner, contributor, viewer, and outsider identities. It
verifies the atomic transition/audit/outbox transaction, a real broker publish, duplicate-safe
notification delivery, assignment, labels, transitions, tenant and role boundaries, pagination,
filtering, stale-version handling, feature quotas, cache invalidation, secure attachment validation,
exact downloads, and cross-workspace file denial. No hosted demo is claimed yet.

## Planned demo script

A deterministic script will expose the already-tested project, work-item, audit, and notification
flow at the terminal. The automated scenario already proves that a second workspace cannot infer
the work item's existence, duplicate messages do not duplicate notifications, and a stale version
returns `409 Conflict`.

The final script will be idempotent, print no tokens or credentials, and include Bash and PowerShell
entry points.
