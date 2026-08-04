# Evidence index

This page separates dated generated evidence from repository configuration and future work. Results apply only to the referenced commit and environment.

## Current verified baseline

The 2026-08-04 local verification for commit `7e15cf45eaa6c3b23ffafec078cf53f8b1d8cb01` is recorded in [issue #3](https://github.com/adnanjalloh/WorkOps.Platform/issues/3) and proposed in [draft PR #5](https://github.com/adnanjalloh/WorkOps.Platform/pull/5).

| Evidence | Result |
| --- | --- |
| Locked restore and repository tools | Passed |
| Formatting verification | Passed |
| Release build | Passed — 0 warnings, 0 errors |
| Automated tests | Passed — 106 total |
| Line coverage | 90.3% |
| Branch coverage | 48.9% |
| Configured coverage floors | Passed — 70% lines / 35% branches |
| Compose host-port boundary | Passed |
| Compose configuration | Passed |
| Docker golden scenario | Passed |

Test distribution: 53 unit, 24 integration, 21 functional, and 8 architecture tests.

## Public hosted evidence

The reviewed default-branch commit has public workflow results for:

- [CI](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727069465);
- [CodeQL](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727069476);
- [full-stack demo](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30727906844).

These links are historical run evidence. Workflow configuration alone is not treated as a timeless passing scan result.

## Evidence map

| Claim area | Primary evidence |
| --- | --- |
| Architecture boundaries | [Architecture](architecture.md), [architecture tests](../tests/WorkOps.ArchitectureTests) |
| Tenant isolation | [ADR 0002](adr/0002-tenant-isolation.md), [middleware](../src/WorkOps.Api/Tenancy/WorkspaceContextMiddleware.cs), [integration tests](../tests/WorkOps.IntegrationTests/TenantQueryFilterTests.cs) |
| Authorization | [Permission mapping](../src/WorkOps.Domain/Tenancy/Permissions.cs), [authorization handler](../src/WorkOps.Api/Authorization/PermissionAuthorizationHandler.cs), [functional tests](../tests/WorkOps.FunctionalTests/TenantIdentityEndpointTests.cs) |
| Concurrency | [Work-item service](../src/WorkOps.Application/WorkItems/WorkItemService.cs), [functional tests](../tests/WorkOps.FunctionalTests/TenantIdentityEndpointTests.cs) |
| Idempotency | [ADR 0006](adr/0006-http-idempotency.md), [project service](../src/WorkOps.Application/Projects/ProjectService.cs) |
| Messaging reliability | [ADR 0003](adr/0003-outbox-delivery.md), [outbox processor](../src/WorkOps.Application/Messaging/OutboxProcessor.cs), [operations](operations.md) |
| Secure attachments | [ADR 0005](adr/0005-file-storage-security.md), [attachment service](../src/WorkOps.Application/Files/AttachmentService.cs), [security controls](security.md) |
| Testing | [Testing strategy](testing.md), [test projects](../tests) |
| Delivery | [CI workflow](../.github/workflows/ci.yml), [release workflow](../.github/workflows/release.yaml), [Dockerfile](../Dockerfile) |
| Local demonstration | [Demo guide](demo.md), [Bash script](../scripts/demo.sh), [PowerShell script](../scripts/demo.ps1) |

## Claim limits

The evidence does not establish production deployment, production users, uptime, throughput, penetration testing, certification, cloud readiness, Kubernetes experience, or production suitability of the local identity, scanner, and file-storage adapters.

Future reports must include their own date, commit, environment, commands, output, and limitations. Do not update a dated claim by editing only the number.
