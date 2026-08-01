# Security

## Honest status

This is the security design baseline for a pre-release portfolio project. Only controls identified
as implemented below exist in code today. The document is informed by OWASP guidance but makes no
certification claim.

## Implemented in the foundation

- nullable reference analysis, recommended .NET analyzers, and warnings as errors;
- explicit health endpoints and centralized Problem Details registration;
- no production secrets or credentials in committed configuration;
- ignore rules for local environment files, keys, certificates, and infrastructure state;
- pinned stable .NET container versions, non-root runtime, dropped capabilities, and read-only root
  filesystem in Compose;
- GitHub Actions with read-only default permissions and third-party actions pinned to full commits;
- configured secret, dependency, and static-analysis workflows;
- clean dependency boundaries checked by tests.

## Required before the first release

- validate JWT issuer, audience, signature, lifetime, and accepted algorithms through a
  provider-neutral OIDC boundary;
- derive workspace context from validated identity plus verified membership, never a body field;
- centralize permissions and use resource-level authorization;
- validate, bound, and map every external contract explicitly;
- define a sanitization policy for each new input surface and never log raw submitted values;
- keep tenant identifiers in database rows, cache keys, file paths, messages, audit events, and
  idempotency records;
- return safe RFC 9457-style Problem Details without stack traces or policy internals;
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
