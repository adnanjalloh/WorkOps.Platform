# ADR 0003: Transactional outbox and idempotent notification delivery

- Status: Accepted
- Date: 2026-08-01

## Context

A work-item transition must not succeed without its audit evidence and notification intent. Writing
to PostgreSQL and RabbitMQ in one distributed transaction would add operational complexity without
removing the need to handle ambiguous network failures. Direct publishing inside the HTTP request
could lose a message after the database commit or publish a message for a rolled-back update.

## Decision

The transition, safe audit event, and outbox message are written by one Entity Framework unit of
work and therefore one PostgreSQL transaction. The outbox payload contains generated identifiers,
state names, a timestamp, and a server-generated correlation identifier; it contains no title,
label, submitted description, token, or credential.

A background worker claims one due message with PostgreSQL `FOR UPDATE SKIP LOCKED`, changes it to
`Processing`, increments its persisted attempt count, and assigns a 30-second lease. It publishes a
durable RabbitMQ message with publisher confirms and a stable message ID. Successful publication
marks the outbox row `Processed`. Failure schedules deterministic exponential backoff with bounded
jitter; the fifth failed attempt moves the row to `Failed` with a generic error code.

The RabbitMQ consumer uses manual acknowledgments and prefetch one. It validates the internal
envelope, establishes an explicit background workspace context, and stores an inbox receipt plus a
development notification in one PostgreSQL transaction. The inbox primary key and notification
unique index include the workspace and message ID. A repeated message therefore produces no second
user-visible effect. Repeated consumer failure routes the message to a durable failed queue.

Owners and administrators may replay a known failed outbox row through a tenant-filtered endpoint.
Other states cannot be replayed, and every accepted replay is audited.

## Guarantees and limits

- Aggregate, audit, and outbox persistence is atomic.
- Broker publication is at-least-once, not exactly-once.
- A crash after broker confirmation and before the processed update can republish the same message.
- The inbox and notification constraints make that publish window harmless for this consumer.
- Short leases and `SKIP LOCKED` allow multiple workers without concurrent processing of one row.
- Five attempts prevent an infinite poison-message loop while preserving an explicit recovery path.
- Independently reordered business events do not yet have an aggregate sequence guard.
- RabbitMQ TLS, production credentials, retention, alerting, and operator access are deployment
  responsibilities and are not claimed by the local Compose environment.

## Consequences

The database contains operational delivery state and requires retention and backlog monitoring.
Consumers must remain idempotent even when the broker is healthy. New message types need an explicit
route, validation policy, safe payload review, consumer, inbox identity, failure policy, and tests.

This design avoids a distributed transaction coordinator and avoids hiding delivery semantics
behind a large messaging framework. The tradeoff is explicit worker, lease, retry, and recovery code
that must be maintained and observed.
