# Demo

## Current demo

The foundation exposes two unauthenticated operational endpoints only:

```text
GET /health/live
GET /health/ready
```

Run the API locally and request `/health/live`. No business API or hosted demo is claimed yet.

## Planned golden scenario

A deterministic script will create synthetic users and two workspaces, assign a contributor, create
a project and work item, perform a version-checked transition, and show the corresponding audit and
notification result. The same demo will prove that a caller from the second workspace cannot infer
the work item's existence and that a stale version returns `409 Conflict`.

The final script will be idempotent, print no tokens or credentials, and include Bash and PowerShell
entry points.
