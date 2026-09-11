# Work-item queries

`GET /api/v1/work-items/` lists work items in the authenticated member's workspace.
Supply a bearer token and `X-Workspace-Id`. Owners, project contributors, and viewers have
read permission; an authenticated non-member receives a non-disclosing `404`.

## Query contract

| Parameter | Default | Behavior |
| --- | --- | --- |
| `page` | `1` | Integer from 1 through 10,000. |
| `pageSize` | `20` | Integer from 1 through 100. |
| `projectId` | No filter | Non-empty project UUID. |
| `status` | No filter | Case-insensitive `Backlog`, `InProgress`, `Blocked`, or `Completed`; numeric enum values are rejected. |
| `assigneeUserId` | No filter | Non-empty user UUID; matches assigned items only. |
| `search` | No filter | Case-insensitive literal substring of the title, normalized and trimmed, at most 120 characters. |

Filters combine with AND. Blank search/status values mean no filter. The shared search policy
rejects control characters, angle brackets, `%`, and `_`; a backslash is searched literally.
Unknown or foreign project/assignee IDs produce an empty result, just like any non-matching
filter. No separate foreign-resource lookup exposes whether those IDs exist.

Malformed typed values return `400`; well-formed values outside the accepted bounds or policies
return `422` with safe Problem Details. Submitted search text is not included in application
rejection diagnostics. Other deployment components, such as access logs, require their own
query-string redaction policy.

Example request (use the named requests in [the HTTP collection](../demo/workops.http) for local tokens and IDs):

```http
GET /api/v1/work-items/?page=1&pageSize=20&status=InProgress&search=tenant
Authorization: Bearer <local-access-token>
X-Workspace-Id: <workspace-id>
```

The response has `items`, `page`, `pageSize`, and `totalCount`. Each item uses the same representation
as single-item retrieval, including labels, assignee display name, and the opaque version token
needed for updates. `totalCount` covers only matching rows in the current workspace before paging.
A page beyond the last item returns an empty `items` array while retaining that matching count.

## Ordering, persistence, and limits

Results are ordered by `CreatedAt DESC, Id DESC`; the UUID breaks ties when creation times match.
Ordering is deterministic for an unchanged dataset. This is bounded offset pagination, not a
snapshot or cursor contract: concurrent inserts, updates, and deletions can shift pages, and the
separate count and page queries can observe different committed states. Refresh after changes.

The query remains server-side, parameterized, tenant-filtered, and no-tracking. Two new B-tree
indexes support workspace-wide and per-project creation-time ordering; existing indexes support
status/assignee filtering. Substring search can still scan matching workspace rows. There is no
claim of full-text ranking, large-scale performance, or an index-only query plan.

Apply the `WorkItemListingIndexes` EF migration using the repository's normal migration workflow.
It adds only indexes; its down migration removes only those indexes. Creating indexes can block
writes on a populated database, so a future production rollout needs a deliberate migration window
or a separately designed concurrent-index procedure. The local demo remains synthetic.

## Verification

[HTTP regression tests](../tests/WorkOps.FunctionalTests/WorkItemListingEndpointTests.cs) cover
defaults, page boundaries, timestamp ties, individual and combined filters, literal backslashes,
empty results, roles, missing context, foreign rows/counts, and invalid query values against the
real ASP.NET Core host and PostgreSQL.
