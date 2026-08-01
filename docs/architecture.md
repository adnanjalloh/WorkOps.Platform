# Architecture

## Current scope

The current milestone establishes the five-project modular-monolith boundary and implements its
first vertical platform capability: identity and tenant isolation. The API validates external JWTs,
resolves an active workspace membership, establishes a request-scoped workspace context, and then
uses tenant-filtered persistence for normal reads.

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
- [ADR 0002 - tenant isolation](adr/0002-tenant-isolation.md)
- Outbox delivery and file storage will each receive an ADR when introduced.
