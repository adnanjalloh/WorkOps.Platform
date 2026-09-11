# .NET servicing alignment and interview demo — 11 September 2026

## Source and environment

Local verification covers commit `589725ae5d4d256bd57ddd8e5b24e14d523c16df`, before this
documentation-only evidence update. The host was macOS arm64 with Docker `29.4.3`, workspace-local
.NET SDK `10.0.401`, EF tool `10.0.12`, and ReportGenerator `5.5.11`. No system-wide SDK was replaced.

The SDK archive's SHA-512 matched [Microsoft's release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json).
The [.NET 10.0.12 release notes](https://github.com/dotnet/core/blob/main/release-notes/10.0/10.0.12/10.0.12.md)
identify SDK `10.0.401` as containing runtime `10.0.12`. Review also covered
[VSTest 18.10.0](https://github.com/microsoft/vstest/releases/tag/v18.10.0),
[EF Core 10.0.12](https://github.com/dotnet/efcore/releases/tag/v10.0.12), and
[OpenAPI.NET 2.12.0](https://github.com/microsoft/OpenAPI.NET/releases/tag/v2.12.0).

## Change verified

- SDK metadata, Docker SDK, and CI/CodeQL/release SDK pins agree on `10.0.401`.
- Runtime, eleven centrally managed ASP.NET Core/EF/Extensions packages, and `dotnet-ef` agree on
  `10.0.12`. Test SDK and its test-platform/coverage dependencies are `18.10.0`.
- An initial restore exposed `NU1109`: ASP.NET Core OpenAPI `10.0.12` requires Microsoft.OpenApi
  at least `2.12.0`. Updating that central pin resolved the downgrade.
- Seven affected lockfiles were regenerated across the solution; unchanged domain/contract locks
  stayed untouched. Reviewed version changes were confined to the related Microsoft dependency graph.
- Demo scripts now check combined filters and foreign result counts on both fresh and saved-state
  paths. Fresh-run nonces distinguish quick successive runs. The speaking guide explains these
  checks and the existing concurrency/recovery evidence without claiming a new recording.

## Local results

| Check | Result |
| --- | --- |
| Locked solution restore and tool restore | Passed |
| Formatting verification | Passed |
| Release build | Passed; zero warnings and errors |
| Unit cases | 61 passed |
| Integration cases | 25 passed |
| Full-host functional cases | 40 passed |
| Architecture cases | 8 passed |
| Total | 134 passed; zero failures or skips |
| EF model consistency | No pending model changes; no new migration required |
| NuGet audit including transitives | No known vulnerable packages reported by the configured source |
| YAML, Bash/PowerShell syntax, relative Markdown links | Passed |
| Compose boundary and SDK bootstrap check | Passed |

ReportGenerator reports 91.3% lines (8,428/9,227), 51.3% branches (648/1,261), and 89.5% methods
(663/740). The gate rounds branches to 51.4%. Existing 70% line and 35% branch floors remain
unchanged; generated migration/snapshot code remains included. These values match the previous
query/recovery source's coverage and do not imply new application behavior or higher scale.

PowerShell syntax was checked using the cached PowerShell `7.4.7` Linux amd64 container on this
arm64 host. That syntax check alone is not evidence of an end-to-end script run or Windows support.

## Reproduction and hosted boundaries

Use the commands in [testing](../testing.md#commands), substituting a fresh result directory such
as `artifacts/dotnet-alignment-verification` for coverage collection/merging. Restore repository
tools with `dotnet tool restore`. The model check is:

```bash
dotnet ef migrations has-pending-model-changes --project src/WorkOps.Infrastructure \
  --startup-project src/WorkOps.Infrastructure --configuration Release --no-build
```

Required CI, CodeQL, dependency review, and the refreshed full-stack workflow are separate hosted
evidence tracked on [PR #67](https://github.com/adnanjalloh/WorkOps.Platform/pull/67). The full-stack
workflow targets fresh and replay paths for both Bash and PowerShell on a Linux runner and screens
all logs before display/upload. The final PR records completed run links; workflow configuration
alone is not a passing result.

No new release, package publication, production deployment, narrated video, Windows test run, or
production-readiness claim is made. Earlier verification reports remain dated historical evidence.
