# Security

## Honest status

This is the security baseline for a pre-release portfolio project. Only controls identified as
implemented below exist in code today. The document is informed by OWASP guidance but makes no
certification claim.

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
- tag-gated release verification, an approval-capable release environment, semantic and
  commit-addressed container tags, SPDX JSON SBOM generation, and digest evidence;
- clean dependency boundaries checked by tests;
- strict bearer-token validation for issuer, audience, signature, expiration, accepted algorithms,
  and the required `sub` claim;
- workspace context selected only from a route or header and accepted only after active membership
  lookup for the validated subject;
- centralized role permissions and policy-based authorization on tenant endpoints;
- non-disclosing `404` for absent or cross-workspace membership and `403` for suspended workspaces;
- default-deny tenant query filters for workspaces and memberships, including the no-context case;
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
  metadata, and cleanup after a failed metadata transaction;
- tenant-filtered attachment metadata, work-item ownership constraints, permission-protected upload
  and download, non-disclosing cross-workspace denial, attachment download responses with
  `X-Content-Type-Options: nosniff`, and no archive support;
- server-generated correlation/trace identifiers in headers and Problem Details, with caller input
  excluded from the correlation scope;
- Serilog JSON events that use structured properties, log input-rejection metadata rather than
  values, and omit exception/provider details from background retry warnings;
- OpenTelemetry instrumentation without user IDs, email, tokens, titles, or submitted values in
  custom metric labels; OTLP export is disabled by default;
- deny-by-default CORS, fixed-window user/IP rate limiting, request-header and body bounds, HSTS and
  HTTPS redirection outside development/test, API security headers, and no server header;
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

## Required before the first release

- replace the development file scanner with a monitored antivirus service and replace ephemeral
  local storage with durable private object storage before production use;
- redact credentials, authorization headers, personal data, and user-controlled content from logs;
- prove cross-workspace denial, privilege boundaries, upload rejection, JWT rejection, and log/error
  redaction through automated tests.
- configure trusted reverse-proxy forwarding deliberately when TLS terminates before the process;
  do not accept forwarded headers from arbitrary networks.

## Secrets

Local developer secrets belong in .NET user-secrets or an ignored `.env` file. Deployed secrets
belong in a managed secret store. `.env.example` contains names and safe values only. A value
committed to Git must be treated as exposed and rotated; deleting the current file is insufficient.
