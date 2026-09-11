# Two-minute interview walkthrough

Use this outline for a live interview or a recording with your own voice. It is a speaking guide,
not a claim that a new narrated video has been recorded. Adapt the words to how you naturally explain
the code, and only describe design decisions you can discuss in detail.

## Prepare the running demo

From the repository root, start the local services before the presentation:

```bash
./scripts/bootstrap.sh
./scripts/demo.sh --start
```

Once that succeeds, record just the repository and terminal. To show the complete flow again,
use a new state-file name for each take:

```bash
WORKOPS_DEMO_STATE=.local/interview-take-01.json ./scripts/demo.sh
```

Change `01` to `02` for another full take. Reusing a state file takes the shorter replay path.
Each full take creates synthetic demo records. The command prints the checks it actually performs;
keep any failures visible and investigate them before describing a successful demonstration.
The [demo guide](demo.md) explains the local services and manual HTTP collection.

## Speaking outline

| Time | Show | Suggested wording |
| --- | --- | --- |
| 0:00–0:15 | README introduction | "I'm Adnan, a .NET backend engineer based in Mannheim. WorkOps is a portfolio API for teams to manage projects and work items in separate workspaces." |
| 0:15–0:35 | Terminal: project replay and viewer denial | "An owner creates a project and invites colleagues. Retrying the same creation request returns the original project. A viewer can read, but a write is rejected with 403." |
| 0:35–0:55 | Terminal: transition and combined filters | "A contributor moves an item into progress, then finds it by project, status, assignee, and title. The same filters in another workspace return no rows or count, so filters cannot bypass data isolation." |
| 0:55–1:15 | Terminal: stale-write and outsider checks | "An edit using an old version is rejected with 409 instead of overwriting newer work. A direct lookup from another workspace gets 404 without revealing that the item exists." |
| 1:15–1:40 | Architecture diagram and audit/notification evidence | "The status change, audit entry, and outgoing message are saved together. A worker publishes through RabbitMQ, and the consumer suppresses duplicate effects. A regression test also covers delivery followed by a failed completion write." |
| 1:40–2:00 | Tests and dated verification report | "I chose one deployable application to keep it straightforward to operate and review. The tests use real infrastructure where specified. This is a synthetic portfolio demo, not a production deployment." |

The timing is a target for the explanation, not a performance claim. Pause on the terminal output
as needed. If showing a recording of an earlier run, identify it as a recording with its date and
commit.

## Code to have open

- [Workspace context middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs): how
  an authenticated user and membership establish the workspace boundary.
- [Work-item service](../src/WorkOps.Application/WorkItems/WorkItemService.cs): updates, state
  changes, filters, and stale-version handling.
- [Query implementation](../src/WorkOps.Infrastructure/WorkItems/WorkItemStore.cs): tenant-filtered
  counts and pages, parameterized title search, and creation-time ordering with a UUID tie-breaker.
- [Outbox processor](../src/WorkOps.Application/Messaging/OutboxProcessor.cs): publication and
  retries.
- [Functional tests](../tests/WorkOps.FunctionalTests/TenantIdentityEndpointTests.cs): the workflow
  and its failure cases.

## Questions to practise

1. Why use a modular monolith here? What evidence would justify splitting out a service?
2. Why is a workspace ID in a request insufficient to authorize access?
3. What happens if the database commit succeeds but RabbitMQ is unavailable?
4. What happens if a consumer processes a message but loses its acknowledgement?
5. How does the caller recover from a 409 without discarding another person's changes?
6. Which parts are real integrations, and which are demonstration adapters?
7. Why can offset pages move when other users insert work? When would you choose cursor pagination?
8. Why is a failed completion write after publication safe to retry, and what do the tests not cover?

The downloadable v0.1.0 video remains a historical asset. The commands and outline here demonstrate
the current code; no new narrated recording is claimed.

Use the [case study](portfolio-case-study.md) and [reviewer guide](reviewer-guide.md) for the
implementation tradeoffs behind these answers.

After the presentation, stop this local stack while retaining the synthetic database:

```bash
./scripts/bootstrap.sh --cleanup
```
