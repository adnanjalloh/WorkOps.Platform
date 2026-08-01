# ADR 0004: OIDC provider boundary

- Status: Accepted
- Date: 2026-08-01

## Context

The portfolio needs realistic authentication without creating a custom password, refresh-token, or
signing-key service. Local development must remain reproducible, while production configuration must
stay provider-neutral and validate externally issued access tokens strictly.

## Decision

The API is an OAuth 2.0 resource server. It validates issuer, audience, signing key, expiration,
required signature, accepted algorithms, and the `sub` claim through ASP.NET Core JWT bearer
authentication. The provider subject maps to an application user; workspace membership and
permissions are always loaded from application persistence rather than trusted from arbitrary token
role or tenant claims.

Compose uses a synthetic Keycloak realm for local development only. The application depends on OIDC
metadata and JWT standards, not Keycloak-specific administration APIs. HTTPS metadata is mandatory
outside Development and Testing. Test tokens use a deterministic test-only key through a replaced
configuration manager and never create a production authentication shortcut.

## Consequences

- Passwords, refresh tokens, signing keys, user registration, and provider administration remain
  outside the application.
- A stolen valid access token remains usable until provider expiry/revocation policy takes effect;
  short lifetimes and provider operations are deployment responsibilities.
- Workspace suspension or membership removal takes effect on the next request because authorization
  state comes from the database, not long-lived token roles.
- Changing providers requires compatible issuer/audience/metadata configuration, not domain changes.

## Evidence

Functional tests cover missing and wrong-audience tokens, required subject mapping, inactive
membership, suspended workspaces, role permissions, and non-disclosing cross-workspace denial.
