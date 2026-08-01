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
```

Run the Compose stack to use the local identity provider and PostgreSQL. The automated functional
suite is the reproducible evidence for the current tenant scenario. No hosted demo is claimed yet.

## Planned golden scenario

A deterministic script will create synthetic users and two workspaces, assign a contributor, create
a project and work item, perform a version-checked transition, and show the corresponding audit and
notification result. The same demo will prove that a caller from the second workspace cannot infer
the work item's existence and that a stale version returns `409 Conflict`.

The final script will be idempotent, print no tokens or credentials, and include Bash and PowerShell
entry points.
