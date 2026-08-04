# Golden-scenario demo

## What it exercises

The live demo runs against the same PostgreSQL, Redis, RabbitMQ, Keycloak, and API adapters used by
Docker Compose. It uses only synthetic local users and data, and checks:

1. owner, contributor, viewer, and outsider authentication;
2. two independent workspace boundaries;
3. contributor and viewer invitation with role-scoped permissions;
4. project creation plus exact `Idempotency-Key` replay;
5. viewer write denial as `403 Forbidden`;
6. assigned and labeled work-item creation, update, and `Backlog -> InProgress` transition;
7. stale-version rejection as `409 concurrency_conflict`;
8. cross-workspace lookup denial as a non-disclosing `404`;
9. visible safe transition audit and outbox-delivered notification.

Tokens are not intentionally printed or persisted by the scripts. Bash passes bearer headers to
`curl` through standard-input configuration rather than child-process arguments. The saved state
contains only synthetic resource IDs and opaque versions. The hosted workflow screens evidence for
credential-like markers and replaces unsafe output with a blocked notice before upload.

## Run it

macOS/Linux prerequisites: Docker Desktop, `curl`, and `jq`.

```bash
./scripts/bootstrap.sh
./scripts/demo.sh --start
```

PowerShell prerequisite: Docker Desktop and PowerShell 7.

```powershell
./scripts/bootstrap.ps1
./scripts/demo.ps1 -Start
```

The bootstrap scripts validate the repository, Docker client and daemon, Compose, host ports,
SDK metadata, and platform-specific demo prerequisites. They do not install software, start
services, change Docker settings, or inspect credentials. A successful validation prints only the
local API and identity URLs, scenario status, proposed evidence path, and explicit next/cleanup
commands.

The first Keycloak import can take up to two minutes. The scripts save successful IDs under the
ignored `.local/` directory. A repeat run reuses those IDs, verifies the current work item, and
rechecks the stale-write and outsider boundaries without duplicating records.

> This stack is for a local, single-user demonstration. API and identity ports bind to loopback;
> PostgreSQL, RabbitMQ, and Redis are reachable only through the private Compose network.

Environment overrides:

| Variable | Default | Purpose |
|---|---|---|
| `WORKOPS_API_URL` | `http://localhost:8080` | API base URL |
| `WORKOPS_IDENTITY_URL` | `http://localhost:8081` | local identity base URL |
| `WORKOPS_DEMO_PASSWORD` | `local-demo-only` | explicit synthetic realm password |
| `WORKOPS_DEMO_STATE` | `.local/demo-state.json` | ignored ID/version state file |

`WORKOPS_API_HOST_HEADER` is available only for reverse-proxy or container-based validation where a
specific API `Host` header is required; normal local runs leave it unset.

Stop the stack while preserving the PostgreSQL volume:

```bash
./scripts/bootstrap.sh --cleanup
```

PowerShell uses `./scripts/bootstrap.ps1 -Cleanup`. These safe cleanup paths preserve named volumes.
Use `docker compose down --volumes` only when you intentionally want to erase synthetic local state.

## Manual HTTP collection

[workops.http](../demo/workops.http) contains named requests whose response fields feed the next
request. Set a fresh `runId`, run the token and user-info requests, then proceed in file order. The
collection intentionally keeps the local-only password visible so no real secret is implied.

The imported users are:

- `demo-owner`
- `demo-contributor`
- `demo-viewer`
- `demo-outsider`

Do not reuse the development realm, client flow, usernames, or password in a deployed environment.

## Trace the implementation

- HTTP entry points: [workspace](../src/WorkOps.Api/Endpoints/WorkspaceEndpoints.cs),
  [project](../src/WorkOps.Api/Endpoints/ProjectEndpoints.cs), and
  [work item](../src/WorkOps.Api/Endpoints/WorkItemEndpoints.cs)
- Tenant context: [middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs)
- Business flow: [work-item service](../src/WorkOps.Application/WorkItems/WorkItemService.cs)
- Reliable delivery: [outbox processor](../src/WorkOps.Application/Messaging/OutboxProcessor.cs) and
  [worker](../src/WorkOps.Infrastructure/Messaging/OutboxWorker.cs)
- Automated twin: [functional golden-flow test](../tests/WorkOps.FunctionalTests/TenantIdentityEndpointTests.cs)

The [illustrated golden-scenario summary](assets/workops-demo.gif) depicts the expected local flow;
it is not a terminal recording and contains no tokens, browser data, private services, or real
organizations. See the [asset provenance record](assets/README.md).
