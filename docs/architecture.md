# Architecture

## Current scope

The current milestone establishes a five-project modular-monolith boundary and proves its basic
dependency direction. Business modules and external adapters are planned, not yet implemented.

| Project | Role | Allowed project dependencies |
|---|---|---|
| `WorkOps.Domain` | Invariants and domain behavior | None |
| `WorkOps.Application` | Use cases and provider-neutral ports | Domain |
| `WorkOps.Infrastructure` | Persistence and external adapters | Application, Domain |
| `WorkOps.Contracts` | Versioned HTTP contracts | None |
| `WorkOps.Api` | HTTP adapter and composition root | All projects |

Architecture tests currently enforce the two highest-value negative rules: Domain has no project
dependency, and Application cannot reference API or Infrastructure. More namespace and feature
rules will be added with the first vertical slice.

## Planned runtime containers

```mermaid
flowchart TB
    User["Authenticated API client"] --> Api["ASP.NET Core API"]
    Api --> Oidc["OIDC provider"]
    Api --> Db[("PostgreSQL")]
    Api --> Cache[("Redis")]
    Api --> Files["Private file storage"]
    Worker["Outbox worker"] --> Db
    Worker --> Broker["Message transport"]
    Broker --> Notifications["Notification handler"]
    Api -. traces and metrics .-> Telemetry["OpenTelemetry collector"]
    Worker -. traces and metrics .-> Telemetry
```

The current Docker Compose file intentionally runs only the API because the other dependencies are
not implemented yet.

## Planned golden-scenario sequence

```mermaid
sequenceDiagram
    actor Member
    participant API
    participant AuthZ as Authorization
    participant DB as PostgreSQL
    participant Worker as Outbox worker
    participant Notify as Notification handler

    Member->>API: Transition work item with expected version
    API->>AuthZ: Check workspace membership and resource permission
    AuthZ-->>API: Allow
    API->>DB: Update item + audit event + outbox message (one transaction)
    DB-->>API: New version
    API-->>Member: 200 OK
    Worker->>DB: Lease pending outbox message
    Worker->>Notify: Publish deterministic message ID
    Notify->>DB: Record idempotent delivery result
    Worker->>DB: Mark outbox message processed
```

Cross-workspace access and stale versions will be tested as non-disclosing denial and `409
Conflict`, respectively.

## Decisions

- [ADR 0001 - modular monolith](adr/0001-modular-monolith.md)
- Tenant isolation, outbox delivery, identity-provider boundary, and file storage will each receive
  an ADR when their implementation is introduced.
