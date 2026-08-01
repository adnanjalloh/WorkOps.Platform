# OWASP ASVS 5.0 evidence map

## Scope

This map uses the stable [OWASP ASVS 5.0.0](https://github.com/OWASP/ASVS/tree/v5.0.0_release/5.0)
chapter and section identifiers. It links relevant areas to repository evidence; it is not a
line-by-line assessment, a Level 1/2/3 claim, or a certification. `Partial` means an application
baseline exists while deployment or broader product concerns remain.

| ASVS 5.0 area | Status | Repository evidence | Residual work |
|---|---|---|---|
| V1 Encoding and Sanitization | Implemented for current HTTP contracts | Named sanitization profiles, request-surface architecture tests, malicious-input unit and functional tests | Re-run coverage whenever a contract or raw input surface is added |
| V2 Validation and Business Logic | Implemented for current vertical slices | Bounded contracts, allowlisted state transitions, role and assignment rules, quota race tests, optimistic concurrency | Expand abuse cases with each new business operation |
| V4.1-V4.2 API and HTTP Message Security | Implemented baseline | Explicit methods/contracts, body and header bounds, safe Problem Details, content types, rate limits, CORS denial, security headers | Proxy and edge configuration require deployment verification |
| V5.1-V5.4 File Handling | Implemented baseline | Filename/media/signature validation, bounded reads, strict text decoding, scanner port, opaque private paths, authorized download tests | Replace the development scanner and ephemeral storage before production use |
| V6.8 Authentication with an Identity Provider | Partial | Provider-neutral OIDC boundary, strict JWT validation, missing/invalid-token tests | Provider MFA, recovery, revocation, and key operations are deployment concerns |
| V8 Authorization | Implemented for current endpoints | Central role-permission map, endpoint policies, active membership checks, default-deny tenant filters, non-disclosing denial tests | Reassess PostgreSQL row-level security as sensitivity or scale increases |
| V9 Self-contained Tokens | Implemented baseline | Issuer, audience, signature, lifetime, algorithm, and subject validation | Token revocation remains an identity-provider concern |
| V10 OAuth and OIDC | Partial | OIDC provider boundary and metadata security rules | Deployed client registration, redirect URI, and provider operations remain external |
| V12 Secure Communication | Partial | Production HTTPS redirect/HSTS, HTTPS-only CORS origins, secure metadata requirement | TLS termination, broker TLS, database TLS, and trusted proxy configuration are deployment work |
| V13 Configuration | Implemented application baseline | Safe committed defaults, ignored secret formats, production validation, locked dependencies, non-root read-only container | Managed secrets and scoped workload identity are deployment work |
| V14 Data Protection | Partial | Tenant isolation, minimal audit/message content, private file paths, safe errors and diagnostics | Data classification, retention, backup, deletion, and regional rules require product decisions |
| V15 Secure Coding and Architecture | Implemented baseline | Modular-monolith dependency tests, warnings as errors, locked restore, CodeQL, dependency review, NuGet audit, image scan | Formal independent review and provenance attestation remain |
| V16 Security Logging and Error Handling | Implemented baseline | Structured JSON logs, generated correlation/trace IDs, safe Problem Details, low-cardinality metrics, tested submitted-value absence | Central retention, alerting, access control, and incident exercises are deployment work |

The detailed control inventory is in [security](security.md), abuse cases are in the
[threat model](threat-model.md), and automated evidence is described in [testing](testing.md).
