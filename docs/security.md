# Security

## Honest status

This is the security baseline for the `v0.1.0` portfolio release. Only controls identified as
implemented below exist in code today. The document is informed by OWASP guidance but makes no
certification claim or production-suitability claim.

## Implemented

- nullable reference analysis, recommended .NET analyzers, and warnings as errors;
- explicit health endpoints and centralized Problem Details registration;
- no production secrets or credentials in committed configuration;
- ignore rules for local environment files, keys, certificates, and infrastructure state;
- pinned stable .NET container versions, non-root runtime, dropped capabilities, and read-only root
  filesystem in Compose;
- GitHub Actions with read-only default permissions and third-party actions pinned to full commits;
- configured secret, dependency, static-analysis, and high/critical container vulnerability scans;
- merged coverage evidence with enforced 70% line and 35% branch regression floors;
- tag-gated read-only release verification, checksummed candidate handoff to a separate protected
  publication job, version and commit-addressed container tags, SPDX JSON SBOM generation, and
  digest evidence; the approval-gated publication path reads the digest back from GHCR and creates
  build-provenance and SPDX SBOM attestations for that exact digest;
- a synthetic local identity realm whose direct-grant users, audience mapping, and shared
  development password are explicitly excluded from production use;
- clean dependency boundaries checked by tests;
- strict bearer-token validation for issuer, audience, signature, expiration, accepted algorithms,
  and exactly one case-sensitive `sub` claim of 1–255 printable ASCII characters (`U+0020` through
  `U+007E`), preserved without trimming or normalization;
- workspace context selected only from a route or header and accepted only after active membership
  lookup for the validated subject;
- centralized role permissions and policy-based authorization on tenant endpoints;
- non-disclosing `404` for absent or cross-workspace membership and `403` for suspended workspaces;
- default-deny tenant query filters plus a metadata-driven save guard for every filtered type,
  including the `Workspace` root; request/background writes require the matching current tenant,
  while root creation requires a matching narrowly scoped provisioning context;
- named sanitization profiles on request surfaces, bounded request bodies, safe error responses, and
  logging that records metadata rather than submitted values;
- tenant-filtered project and work-item queries backed by composite workspace ownership constraints;
- invitation role limits and active-current-workspace validation for work-item assignment;
- allowlisted state transitions, priorities, labels, search terms, and page bounds;
- opaque PostgreSQL `xmin` concurrency tokens with stale updates mapped to `409 Conflict`;
- tenant-filtered audit, outbox, inbox, and notification data with composite ownership constraints;
- audit metadata and broker payloads restricted to generated identifiers, state names, field names,
  correlation IDs, and other non-content metadata;
- validated internal message envelopes with durable RabbitMQ routing and publisher confirms;
- tenant-scoped inbox and notification uniqueness constraints that suppress duplicate effects;
- bounded outbox attempts, safe generic failure codes, a failed-message queue, and no raw exception
  persistence;
- owner/administrator-only audit queries and failed-outbox replay, with replay itself audited;
- Redis feature cache keys derived from the established workspace context, explicit write
  invalidation, short expiry, low-cardinality metrics, and database fallback;
- an active-project quota enforced on a tenant-owned PostgreSQL row with optimistic concurrency,
  preventing two concurrent requests from both consuming the final slot;
- attachment filenames and media types sanitized with allowlists, a 512 KiB per-file limit checked
  before and during buffering, and strict extension/media-type/signature agreement;
- PDF and PNG magic-byte checks, strict UTF-8/control-character checks for text, a scanner port that
  fails closed by default, and a deliberately named development-only clean scanner;
- opaque server-generated storage names, tenant-separated paths outside the web root, SHA-256
  metadata, best-effort cleanup after a failed metadata transaction, safe missing/corrupt-content
  handling, and an operator reconciliation tool;
- tenant-filtered attachment metadata, work-item ownership constraints, permission-protected upload
  and download, non-disclosing cross-workspace denial, attachment download responses with
  `X-Content-Type-Options: nosniff`, and no archive support;
- server-generated correlation/trace identifiers in headers and Problem Details, with caller input
  excluded from the correlation scope;
- Serilog JSON events that use structured properties and log input-rejection metadata rather than
  values; outbox publish failures emit a sanitized message ID, allowlisted type, result, and failure
  category without the exception object, payload, token, or connection string;
- OpenTelemetry instrumentation without user IDs, email, tokens, titles, or submitted values in
  custom metric labels; versions derive from the release build, OTLP export is disabled by default,
  and production transport requires HTTPS or explicit internal-only cleartext opt-in;
- deny-by-default CORS, fixed-window user/IP rate limiting, request-header and body bounds, HSTS and
  HTTPS redirection outside development/test, explicit trusted-proxy forwarding, API security
  headers, and no server header;
- runtime OpenAPI document exposure only in Development, with no Swagger UI installed;
- optional project-create idempotency scoped by tenant, authenticated user, method, route, and key;
  request hashes prevent key reuse with changed input and the database primary key protects the
  first-write boundary;
- PostgreSQL-backed tests for tenant filtering plus HTTP tests for token rejection, cross-workspace
  denial, inactive membership, suspension, capabilities, malicious input, assignment boundaries,
  invalid state changes, concurrent-update conflicts, audit authorization, duplicate delivery,
  cache/file isolation, quota races, path traversal, MIME/signature mismatch, invalid UTF-8, and
  oversized uploads, untrusted origins, rate limits, production HSTS/OpenAPI behavior, safe
  diagnostics, tested-log redaction, and idempotency replay/mismatch behavior.

The [OWASP ASVS 5.0 map](asvs-map.md) connects implemented controls to review evidence. It is a
navigation aid, not a certification or a claim that every requirement in a referenced area passes.

The demo scripts write only synthetic IDs and opaque versions to the ignored `.local/` directory;
tokens are not intentionally printed or persisted. The `.http` collection contains only the
declared local-only password.

The public `v0.1.0` image has independently verified build-provenance and SPDX SBOM attestations for
digest `sha256:0297c341cf86d056163e167a71ea4789d316bbb0ecaaf2950ce69f3e20debd5a`.
GitHub artifact attestations establish provenance and integrity for that digest; they do not
establish a separate traditional code-signing mechanism.

## Required before production deployment

- replace the development file scanner with a monitored antivirus service and replace ephemeral
  local storage with durable private object storage before production use;
- redact credentials, authorization headers, personal data, and user-controlled content from logs;
- keep cross-workspace denial, privilege boundaries, upload rejection, JWT rejection, and log/error
  redaction in the automated regression suites;
- configure the disabled-by-default forwarded-header trust list for the deployment proxy/network
  when TLS terminates before the process.

## Secrets

Local developer secrets belong in .NET user-secrets or an ignored `.env` file. Deployed secrets
belong in a managed secret store. `.env.example` contains names and safe values only. A value
committed to Git must be treated as exposed and rotated; deleting the current file is insufficient.
