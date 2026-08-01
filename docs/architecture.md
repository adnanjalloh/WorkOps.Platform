# Architecture

## Current scope

The current milestone carries the first business slice through reliable asynchronous delivery. The
API validates external JWTs, establishes tenant context, and uses tenant-filtered persistence for
projects, work items, audit events, outbox/inbox records, and notifications. Domain behavior owns
project archiving, work-item transitions, and the outbox failure state.

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
    Worker["Leased outbox worker"] --> Db
    Worker --> Broker["RabbitMQ"]
    Broker --> Notifications["Idempotent notification handler"]
    Notifications --> Db
    Api -. traces and metrics .-> Telemetry["OpenTelemetry collector"]
    Worker -. traces and metrics .-> Telemetry
```

Docker Compose currently runs the API, PostgreSQL, RabbitMQ, and a local identity provider with an
imported synthetic realm. Redis, file storage, and telemetry remain future milestones.

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

## Implemented golden-scenario sequence

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
    API->>DB: Update item + safe audit + outbox (one transaction)
    DB-->>API: New version
    API-->>Member: 200 OK
    Worker->>DB: Lease with FOR UPDATE SKIP LOCKED
    Worker->>Notify: Publish confirmed message with stable ID
    Notify->>DB: Insert inbox + notification atomically
    Worker->>DB: Mark outbox message processed
```

The transport is deliberately at-least-once. A crash after broker confirmation but before the
outbox row is marked processed can publish the same message again; the tenant-scoped inbox key and
notification uniqueness constraint turn that retry into a no-op. Claims use short leases and
`FOR UPDATE SKIP LOCKED`; failures use deterministic exponential backoff with jitter, stop after
five attempts, and remain recoverable through a protected, audited replay endpoint. The RabbitMQ
consumer retries once before routing a persistent failure to a durable failed-message queue.

Cross-workspace access, stale versions, concurrent claims, real broker routing, and duplicate
delivery are covered by automated tests. Full tracing and exported metrics are planned for the
production-hardening milestone; the current code emits low-cardinality messaging result counters.

## Decisions

- [ADR 0001 - modular monolith](adr/0001-modular-monolith.md)
- [ADR 0002 - tenant isolation](adr/0002-tenant-isolation.md)
- [ADR 0003 - outbox delivery](adr/0003-outbox-delivery.md)
- File storage will receive an ADR when introduced.
