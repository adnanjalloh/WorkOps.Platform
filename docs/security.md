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
- configured secret, dependency, and static-analysis workflows;
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
- PostgreSQL-backed tests for tenant filtering plus HTTP tests for token rejection, cross-workspace
  denial, inactive membership, suspension, capabilities, malicious input, assignment boundaries,
  invalid state changes, and concurrent-update conflicts.

## Required before the first release

- keep tenant identifiers in database rows, cache keys, file paths, messages, audit events, and
  idempotency records;
- allowlist upload types, inspect signatures, generate server-side names, and authorize downloads;
- add request-size limits, explicit CORS, HTTPS enforcement in deployed environments, rate limits,
  and production-safe OpenAPI behavior;
- redact credentials, authorization headers, personal data, and user-controlled content from logs;
- prove cross-workspace denial, privilege boundaries, upload rejection, JWT rejection, and log/error
  redaction through automated tests.

## Secrets

Local developer secrets belong in .NET user-secrets or an ignored `.env` file. Deployed secrets
belong in a managed secret store. `.env.example` contains names and safe values only. A value
committed to Git must be treated as exposed and rotated; deleting the current file is insufficient.
