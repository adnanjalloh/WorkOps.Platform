# ADR 0001: Use a modular monolith

- Status: Accepted
- Date: 2026-08-01

## Context

The portfolio must show senior backend judgment while remaining reviewable in minutes. Multiple
deployables would add operational and cognitive cost before any implemented use case requires
independent scaling or deployment.

## Decision

Build one deployable ASP.NET Core application with explicit Domain, Application, Infrastructure,
Contracts, and API boundaries. Organize business behavior into vertical feature slices inside these
boundaries. Run reliable background processing in the same deployable for the first release.

Enforce dependency direction with tests. Provider-specific code stays in Infrastructure; HTTP and
configuration composition stays in API; public contracts do not expose persistence entities.

## Consequences

The solution is easy to build, test, run, and review. Database transactions can cover domain change,
audit, and outbox persistence without distributed transactions. Module discipline must be enforced
in code because process isolation does not provide it automatically.

If measured operational needs later justify a separate worker or service, the existing ports and
message contracts provide an extraction seam. Extraction is not a roadmap goal by itself.
