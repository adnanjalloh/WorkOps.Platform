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
| 0:00–0:20 | README introduction | "I'm Adnan, a .NET backend engineer based in Mannheim. WorkOps is a portfolio API for teams to manage projects and work items. I use it to demonstrate permissions, data isolation, and reliable processing." |
| 0:20–0:45 | Terminal: project replay and viewer denial | "An owner creates a project and adds team members. If the same creation request is retried, it returns the original project. A viewer can read but cannot create a project; the API returns 403." |
| 0:45–1:10 | Terminal: work-item transition, 409, and 404 | "A contributor moves a work item into progress. An update using an old version is rejected with 409. Someone in another workspace gets 404, so the response doesn't reveal that this item exists." |
| 1:10–1:35 | Architecture diagram and outbox processor | "The change, audit entry, and outbox message are saved in one database transaction. A worker publishes the message through RabbitMQ. The consumer handles duplicate delivery. The demo waits until the audit and notification are visible." |
| 1:35–2:00 | Tests and dated verification report | "I chose a modular monolith to keep the system straightforward to run and review. The tests cover permissions, concurrent edits, tenant boundaries, and messaging. This is a local portfolio demonstration; the identity, storage, and scanner setup would need production replacements." |

The timing is a target for the explanation, not a performance claim. Pause on the terminal output
as needed. If showing a recording of an earlier run, identify it as a recording with its date and
commit.

## Code to have open

- [Workspace context middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs): how
  an authenticated user and membership establish the workspace boundary.
- [Work-item service](../src/WorkOps.Application/WorkItems/WorkItemService.cs): updates, state
  changes, and stale-version handling.
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

Use the [case study](portfolio-case-study.md) and [reviewer guide](reviewer-guide.md) for the
implementation tradeoffs behind these answers.

After the presentation, stop this local stack while retaining the synthetic database:

```bash
./scripts/bootstrap.sh --cleanup
```
