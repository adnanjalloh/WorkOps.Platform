# Architecture

## Current scope

The current milestone extends the five-project modular monolith through its first business slice.
The API validates external JWTs, resolves active workspace membership, establishes request-scoped
tenant context, and uses tenant-filtered persistence for projects and work items. Domain behavior
now owns project archiving and the work-item transition state machine.

| Project | Role | Allowed project dependencies |
|---|---|---|
| `WorkOps.Domain` | Invariants and domain behavior | None |
| `WorkOps.Application` | Use cases and provider-neutral ports | Domain |
| `WorkOps.Infrastructure` | Persistence and external adapters | Application, Domain |
| `WorkOps.Contracts` | Versioned HTTP contracts | None |
| `WorkOps.Api` | HTTP adapter and composition root | All projects |

Architecture tests enforce that Domain has no project dependency, Application cannot reference API,
Contracts, or Infrastructure, tenant-owned entities declare their ownership boundary, and every
request string has a sanitization policy or an explicit skip reason.

## Current and planned runtime containers

```mermaid
flowchart TB
    User["Authenticated API client"] --> Api["ASP.NET Core API"]
    Api --> Oidc["OIDC provider"]
    Api --> Db[("PostgreSQL")]
    Api -. planned .-> Cache[("Redis")]
    Api -. planned .-> Files["Private file storage"]
    Worker["Planned outbox worker"] -.-> Db
    Worker -.-> Broker["Planned message transport"]
    Broker --> Notifications["Notification handler"]
    Api -. traces and metrics .-> Telemetry["OpenTelemetry collector"]
    Worker -. traces and metrics .-> Telemetry
```

Docker Compose currently runs the API, PostgreSQL, and a local identity provider with an imported
synthetic realm. Redis, message transport, file storage, and telemetry remain future milestones.

## Tenant request path

```mermaid
sequenceDiagram
    actor Client
    participant API
    participant JWT as JWT validator
    participant Access as Membership boundary
    participant DB as PostgreSQL

    Client->>API: Request + bearer token + workspace ID
    API->>JWT: Validate issuer, audience, signature, lifetime, algorithm, subject
    JWT-->>API: Validated subject
    API->>Access: Resolve active membership for subject and workspace
    Access->>DB: Deliberate unfiltered boundary query
    Access-->>API: Role and workspace status
    API->>DB: Tenant-filtered application query
    API-->>Client: Resource, 403 suspended, or non-disclosing 404
```

Project and work-item rows carry a non-null workspace identifier. A composite foreign key prevents
a work item from pointing to a project in another workspace, while assignment is accepted only for
an active member of the current workspace.

## Implemented work-item sequence

```mermaid
sequenceDiagram
    actor Contributor
    participant API
    participant Domain
    participant DB as PostgreSQL

    Contributor->>API: Update or transition + expected version
    API->>DB: Load through tenant filter
    API->>Domain: Validate update or state transition
    Domain-->>API: Accepted domain state
    API->>DB: UPDATE row WHERE xmin = expected version
    alt version matches
        DB-->>API: New xmin token
        API-->>Contributor: 200 OK
    else stale version
        DB-->>API: No matching row version
        API-->>Contributor: 409 Conflict
    end
```

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
    API->>DB: Update item + audit event + outbox message (one transaction, planned)
    DB-->>API: New version
    API-->>Member: 200 OK
    Worker->>DB: Lease pending outbox message
    Worker->>Notify: Publish deterministic message ID
    Notify->>DB: Record idempotent delivery result
    Worker->>DB: Mark outbox message processed
```

Cross-workspace access and stale versions are tested as non-disclosing denial and `409 Conflict`,
respectively. Audit persistence, outbox delivery, and idempotent notification handling are the next
milestone.

## Decisions

- [ADR 0001 - modular monolith](adr/0001-modular-monolith.md)
- [ADR 0002 - tenant isolation](adr/0002-tenant-isolation.md)
- Outbox delivery and file storage will each receive an ADR when introduced.
