# Operations

## Current endpoints

- `/health/live` reports whether the process can serve requests and intentionally runs no dependency
  checks.
- `/health/ready` verifies PostgreSQL connectivity and, when enabled, RabbitMQ and Redis
  connectivity. It reports unhealthy when a required dependency is unavailable.

## Local commands

```bash
dotnet run --project src/WorkOps.Api
docker compose config
docker compose up --build
docker compose down
```

Standalone hosts leave `Messaging:Enabled` false unless a broker is deliberately configured.
Docker Compose enables RabbitMQ, applies the database migration, starts the leased outbox worker,
starts the notification consumer, and enables authenticated Redis caching. It also enables a local
clean-scanner stub and stores attachments in `/tmp/workops-attachments` inside the API container.
That directory is temporary and is not a production durability design.

The local Keycloak realm contains four synthetic demo users and an audience mapper for the
`workops-cli` client. `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` lets the API fetch discovery and JWKS data
through the private Compose hostname while validating the public local issuer. This password-grant
realm exists only for the local scripted demo and must not be promoted to a deployment.

## Cache and attachment configuration

`Cache:Enabled` is false by default. When enabled, `ConnectionStrings:Redis` is required. Cache
outages fall back to PostgreSQL for entitlement reads and never bypass the PostgreSQL quota.

`Files:RootPath` must be absolute. `Files:DevelopmentScannerEnabled` is false by default, causing
uploads to fail closed with `503`. Compose enables the stub only for local demonstration. A deployed
environment must provide private durable storage, backups/retention, malware scanning, and alerts
for missing objects or scanner failures before attachments are considered production-ready.

## Message delivery and recovery

Outbox messages move through `Pending`, `Processing`, `Processed`, and `Failed`. A worker claims one
eligible row with a 30-second lease and PostgreSQL `FOR UPDATE SKIP LOCKED`. Publish failures use
deterministic exponential backoff with jitter and stop after five attempts. Only a generic failure
code is stored. Each publish failure emits one sanitized operator diagnostic containing the message
ID, an allowlisted message type, retry/failure result, and a coarse failure category; the publisher
exception, payload, token, and connection string are not passed to that diagnostic.

RabbitMQ deliveries use manual acknowledgments and prefetch one. The consumer retries a transient
handler failure once, then routes a repeated or invalid delivery to
`workops.notifications.failed.v1`. The handler writes its inbox receipt and development
notification in one database transaction, so a redelivery with the same tenant/message/consumer
key has no additional effect.

An owner or administrator may replay a known failed outbox message with:

```text
POST /api/v1/operations/outbox/{messageId}/replay
X-Workspace-Id: {workspaceId}
```

The endpoint cannot replay pending, processing, or processed messages. Every accepted replay writes
an audit event. Failed-queue inspection and republishing remain restricted broker-operator tasks;
message bodies and credentials must not be copied into tickets or logs.

The current code emits low-cardinality message result, outbox duration, and outbox backlog metrics.

## Diagnostics and HTTP controls

Every response returns `X-Correlation-Id`; Problem Details also include `correlationId` and
`traceId`. Search the structured JSON logs using that value, then follow the trace through ASP.NET
Core, outbound HTTP, PostgreSQL, and `WorkOps.Messaging`. Audit and message rows retain the same
server-generated correlation identifier where the business transaction records one.

Set `Observability:Otlp:Enabled=true` and an absolute HTTPS `Observability:Otlp:Endpoint` to export
traces and metrics. Optional exporter headers are accepted only over HTTPS. Cleartext OTLP requires
the explicit `AllowInsecureTransport` setting and, in production, is limited to a loopback, private,
single-label, or `.internal` collector. Export is disabled by default. Service, meter, and activity-
source versions come from the assembly informational version set by the release build. Do not add
user IDs, email, tokens, work-item titles, raw URLs, or submitted values as labels.

Rate limiting defaults to 60 requests per 60 seconds per authenticated subject, or per remote IP
before authentication. Health endpoints are exempt. `Cors:AllowedOrigins` is empty by default;
production origins must be explicit HTTPS origins. OpenAPI JSON is available only in Development at
`/openapi/v1.json`; no interactive UI is installed.

Forwarded headers are disabled by default. When the API is behind a reverse proxy, enable
`ForwardedHeaders:Enabled` only with a bounded `ForwardLimit` and explicit `KnownProxies` IP addresses
or `KnownNetworks` CIDR ranges. The middleware processes only forwarded client IP and scheme, before
HTTPS redirection and rate limiting; headers from every other source are ignored.

Project creation supports an optional `Idempotency-Key` header. Exact retries return the stored
`201` response and `Idempotency-Replayed: true`; changed input returns
`idempotency_key_conflict`. Records expire after 24 hours. A hosted retention worker purges expired
rows in `SKIP LOCKED` batches, caps batches per run, and emits low-cardinality result and purged-row
metrics. `Idempotency:PurgeBatchSize`, `PurgeIntervalMinutes`, and `MaximumBatchesPerRun` are bounded;
disabling the worker requires an equivalent operator retention process.

Run a conservative authenticated local smoke check without printing the token:

```bash
WORKOPS_ACCESS_TOKEN=... WORKOPS_WORKSPACE_ID=... ./scripts/load-smoke.sh
```

The script defaults to 20 sequential feature reads and labels its output as a local smoke result,
not a production benchmark.

Run the complete local backend scenario with `./scripts/demo.sh --start` or
`./scripts/demo.ps1 -Start`. Successful resource IDs and opaque versions are stored under the
ignored `.local/` directory; access tokens are not intentionally printed or persisted by the scripts.

### Attachment reconciliation

The metadata transaction removes a newly stored object when its database commit fails. If that
best-effort cleanup also fails, an operator log records only the generated attachment ID and the
original database exception is preserved. Downloads validate stored length and SHA-256 metadata;
missing or corrupt content returns a safe `503 attachment_content_unavailable` and emits an operator
alert without logging filenames or content.

Run the bounded reconciliation tool with an absolute `WORKOPS_FILES_ROOT` and standard libpq
`PGHOST`, `PGPORT`, `PGDATABASE`, and `PGUSER` configuration:

```bash
./scripts/reconcile-attachments.sh --report
```

It lists opaque database paths whose objects are missing and opaque storage paths with no metadata
row. After reviewing the report and database backup, `--delete-orphans` removes only paths matching
the generated tenant/storage-name format. It never deletes database rows or attempts to reconstruct
missing content.

## Release process

The release workflow accepts only `vMAJOR.MINOR.PATCH` tags at the current `master` head. A read-only
job restores locked dependencies, verifies formatting, builds, tests with the same coverage gate as
CI, audits NuGet packages, scans Git history, builds and scans the container, generates an SPDX JSON
SBOM, and packages a checksummed candidate. Only after it succeeds does the protected publication
job receive write permissions, load that exact candidate, publish the version and commit tags to
GHCR, read both tags back from the registry, and require them to resolve to the same immutable
digest. The publication job creates GitHub build-provenance and SPDX SBOM attestations for that
digest, pushes the attestation bundles to GHCR, and creates the release with its prepared notes,
SBOM, and digest evidence attached. Optional artifact-metadata storage records are deliberately
disabled because this repository is owned by a personal account.

The attestations are provenance and integrity evidence generated with a short-lived workflow
identity. They are not a claim that a separate traditional code-signing mechanism exists.

With GitHub CLI authentication, verify the public `v0.1.0` image's build provenance:

```bash
gh attestation verify \
  oci://ghcr.io/adnanjalloh/workops.platform:v0.1.0 \
  -R adnanjalloh/WorkOps.Platform
```

Verify the SPDX SBOM attestation separately because it uses a non-default predicate type:

```bash
gh attestation verify \
  oci://ghcr.io/adnanjalloh/workops.platform:v0.1.0 \
  -R adnanjalloh/WorkOps.Platform \
  --predicate-type https://spdx.dev/Document/v2.3
```

Both commands passed on 2026-08-04 for digest
`sha256:0297c341cf86d056163e167a71ea4789d316bbb0ecaaf2950ce69f3e20debd5a`. The
version and commit-addressed tags resolved anonymously to that same registry digest. Repeat all
checks and record new evidence for every later release.

Configure the `release` environment before publishing and add a required reviewer only when that
reviewer is genuinely independent. Treat tags as immutable and protect `v*` tags from update or
deletion. Repository-side controls that cannot be committed are listed in
[GitHub settings](github-settings.md).
