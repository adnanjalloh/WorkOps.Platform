# Evidence index

This page separates dated generated evidence from repository configuration and future work. Results apply only to the referenced commit and environment.

## Local work-item query and recovery verification — 2026-09-11

The [query/recovery report](verification/2026-09-11-work-item-queries.md) records all 134 cases
passing, a zero-warning Release build, locked restore, coverage gates, and model/migration checks.
It identifies the tested source commit and the completion-timestamp bug reproduced and fixed by
the new recovery test. Hosted checks remain separate evidence on the implementation PR.

## Local dependency repair verification — 2026-09-11

The [dependency repair report](verification/2026-09-11.md) records the updated OpenTelemetry/Redis
graph passing locked restore, formatting, a zero-warning Release build, all 106 tests, and coverage
gates on macOS arm64 with .NET SDK `10.0.400`. The NuGet audit reported no vulnerable packages.
The report identifies the tested source commit and distinguishes local checks from hosted merge
gates and release evidence.

## Hosted application verification — 2026-09-09

Application commit `0146705ff95b05e3a382130976bad2a296756f17` passed a
[fresh CI rerun](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/33873683288/attempts/2)
on 2026-09-09. It also passed the
[full-stack demo](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/34328693950) that day
and [CodeQL](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/34105987836) on 2026-09-07.

The [retained verification summary](verification/2026-09-09.md) records the tested commit, run
attempt, 106 passing tests, coverage, completed gates, and artifact hashes. It remains readable
when the downloadable workflow artifacts expire. These results apply to the specified application
commit; the older release below remains a separate artifact.

## Historical baseline — 2026-08-04

The 2026-08-04 local verification for commit `7e15cf45eaa6c3b23ffafec078cf53f8b1d8cb01`
is recorded in [issue #3](https://github.com/adnanjalloh/WorkOps.Platform/issues/3) and
[merged PR #5](https://github.com/adnanjalloh/WorkOps.Platform/pull/5).

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

## Repository settings verification

On 2026-08-04, private vulnerability reporting returned `enabled: true` through the GitHub
repository API after enablement. The configured social preview was visually verified in repository
settings against `docs/assets/workops-social-preview.png`. These observations cover only those two
settings and do not imply that every item in the repository-settings checklist is complete.

## Verified `v0.1.0` release evidence

The protected [release workflow](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/30901202020)
completed on 2026-08-04 for tag `v0.1.0` and commit
`ed44d5248baf268137fedce75cf6b0c39bf3044a`.

| Evidence | Verified result |
| --- | --- |
| GitHub Release | [v0.1.0](https://github.com/adnanjalloh/WorkOps.Platform/releases/tag/v0.1.0), published and not a prerelease |
| Public package | [`ghcr.io/adnanjalloh/workops.platform`](https://github.com/users/adnanjalloh/packages/container/package/workops.platform), linked to this repository |
| Version tag | `ghcr.io/adnanjalloh/workops.platform:v0.1.0` |
| Commit tag | `ghcr.io/adnanjalloh/workops.platform:sha-ed44d5248baf268137fedce75cf6b0c39bf3044a` |
| Registry digest | `sha256:0297c341cf86d056163e167a71ea4789d316bbb0ecaaf2950ce69f3e20debd5a` for both tags |
| Release assets | Digest evidence plus an SPDX 2.3 JSON SBOM; downloaded asset digests matched GitHub metadata |
| Build provenance | One attestation verified for the public registry digest |
| SPDX SBOM | One `https://spdx.dev/Document/v2.3` attestation verified for the same digest |

The registry digest was resolved without stored registry credentials. Both attestations were
verified with GitHub CLI against `adnanjalloh/WorkOps.Platform`. These attestations establish
provenance and integrity for the named image digest; they are not a separate traditional
code-signing mechanism or evidence of a production deployment.

## Public hosted evidence

Application commit `0146705`, named in the 2026-09-09 hosted report, has public workflow results for:

- [CI, attempt 2](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/33873683288/attempts/2);
- [CodeQL](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/34105987836);
- [full-stack demo](https://github.com/adnanjalloh/WorkOps.Platform/actions/runs/34328693950).

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
