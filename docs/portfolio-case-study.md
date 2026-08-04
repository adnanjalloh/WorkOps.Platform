# WorkOps.Platform portfolio case study

## Problem

Backend portfolio projects often demonstrate endpoint construction while leaving authorization, tenant isolation, failure handling, delivery guarantees, and operational evidence implicit. WorkOps.Platform was built to make those harder concerns reviewable in one coherent workflow system.

The goal is not to imitate a large production estate. The goal is to show how a senior backend engineer can turn ambiguous platform requirements into explicit boundaries, executable invariants, and repeatable delivery evidence.

## Constraints

- One public repository must remain understandable without manufacturing a microservice topology.
- Tenant context must come from authenticated identity plus active membership, never from a caller-supplied tenant identifier alone.
- Business writes, safe audit records, and integration messages must not drift across separate transactions.
- At-least-once delivery must not create duplicate user-visible effects.
- External input, diagnostics, file handling, and local credentials must remain bounded and safe for a public synthetic demo.
- Claims must stay tied to generated evidence and must not imply production deployment or certification.

## Architecture decision

The project uses a modular monolith with five explicit projects:

- Domain owns invariants and state transitions.
- Application owns use cases and provider-neutral ports.
- Infrastructure owns persistence and external adapters.
- Contracts owns versioned HTTP request and response types.
- API owns HTTP concerns and composition.

Architecture tests enforce dependency direction and several security-relevant rules. This keeps the system small enough to review while preserving seams for the database, broker, cache, identity provider, scanner, storage, and telemetry exporter.

## Key decisions and tradeoffs

### Tenant isolation

Workspace context is established from a validated subject and active membership. EF Core query filters provide default-deny reads, while a model-driven save guard and composite ownership constraints protect writes.

Tradeoff: this is application-enforced isolation rather than PostgreSQL row-level security. The threat model documents that residual boundary and the tests cover cross-workspace read and write paths.

### Concurrency and idempotency

Work-item versions use opaque PostgreSQL concurrency tokens. Stale writes fail with `409 Conflict`. Project creation accepts an optional idempotency key scoped by tenant, authenticated user, method, route, and request hash.

Tradeoff: idempotency records require bounded retention and an operator process. A hosted purge worker enforces the local policy, and a deployed system must monitor it.

### Reliable messaging

Business state, safe audit data, and outbox messages are committed atomically. A leased worker publishes with confirmation and deterministic retry behavior. The consumer uses manual acknowledgments and stores inbox receipts with the development notification effect.

Tradeoff: at-least-once delivery remains visible in the design. Duplicate suppression is explicit; the system does not claim exactly-once transport.

### Caching and quotas

Redis caches tenant-scoped entitlement snapshots, while PostgreSQL remains authoritative for quota reservations.

Tradeoff: cache failure falls back to the database and favors correctness over availability gains from stale cached authorization or quota data.

### Attachments

Uploads are bounded, filenames and media/signature combinations are validated, scanning is a fail-closed port, and storage uses opaque tenant-separated paths outside the web root.

Tradeoff: the included development scanner and temporary local storage are demonstration adapters. Durable object storage, monitored malware scanning, retention, recovery, and reconciliation alerts remain production requirements.

## Verification strategy

The repository combines four test layers:

- unit tests for domain and application behavior;
- real PostgreSQL, Redis, RabbitMQ, and local-storage integration tests;
- full-host functional tests for identity, authorization, tenancy, HTTP behavior, and cross-component flows;
- architecture tests for dependency and mechanically enforceable safety rules.

The Docker golden scenario provides an operator-friendly synthetic path through authentication, membership, idempotent creation, role denial, concurrency conflict, tenant isolation, audit, and notification delivery.

See [testing](testing.md), the [demo guide](demo.md), and the [evidence index](evidence.md) for dated results.

## Delivery and operational design

The repository includes locked dependencies, warning-free Release builds, pinned workflow actions, CI coverage gates, CodeQL, dependency review, secret scanning configuration, container scanning, an SPDX SBOM path, structured logging, correlation IDs, health checks, and OpenTelemetry instrumentation.

These are configured and tested delivery controls, not evidence of a hosted production service. Release publication remains intentionally gated.

## Lessons

1. A small architecture is easier to trust when every boundary has code, tests, and a documented tradeoff.
2. Tenant safety needs both read filtering and write enforcement; relying on a request header or query filter alone is insufficient.
3. Reliable messaging is a lifecycle and operations problem, not only an outbox table.
4. Public portfolio evidence is stronger when failure cases—`403`, `404`, `409`, retry, replay, and duplicate delivery—are first-class demonstrations.
5. Honest adapter boundaries improve credibility. Local identity, scanning, storage, and telemetry choices should be named rather than disguised as production services.

## Next steps

The next portfolio milestones are tracked as focused issues and pull requests. Higher-value production-boundary work includes a durable object-storage adapter, a real scanner adapter, a local observability stack, richer member lifecycle behavior, work-item search, recovery evidence, and reproducible local performance evidence.

Tags, releases, packages, and cloud resources require separate approval and generated evidence.
