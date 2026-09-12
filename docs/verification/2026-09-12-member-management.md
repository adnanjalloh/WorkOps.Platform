# Workspace member management verification — 2026-09-12

## Source and environment

- Implementation source: `97e408fdc6c18cb5159691ecc7e91a57aa46af18`
- Scope: [issue #72](https://github.com/adnanjalloh/WorkOps.Platform/issues/72), [PR #73](https://github.com/adnanjalloh/WorkOps.Platform/pull/73)
- Host: macOS arm64; workspace-local .NET SDK `10.0.401`
- Runtime/platform packages: `10.0.12`; dependency graph unchanged by this feature
- PostgreSQL, RabbitMQ, and Redis tests use real, isolated Testcontainers instances

This report is added after verification. Any later documentation-only commit retains the tested
application, test, migration, dependency, and workflow trees; hosted final-head checks are linked
on the PR separately. The new routes are role changes and deactivation, not reactivation or
external identity-provider administration.

## Local results

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Formatting verification | Passed |
| Release build | Passed, zero warnings/errors |
| Unit tests | 65 passed |
| Integration tests | 41 passed |
| Functional tests | 46 passed |
| Architecture tests | 8 passed |
| Total | 160 passed, zero failures/skips |
| Additional owner/version race repetitions | 12/12 passed across three extra isolated runs |
| Coverage gate | Passed: 92.2% lines, 53.9% branches; unchanged 70%/35% floors |
| Transitive NuGet audit | No known vulnerable packages reported across all nine projects |
| Model consistency | No pending model changes |
| Compose host-port boundary | Passed |
| Changed Markdown relative links | Resolve to existing repository paths |

The final merged Cobertura artifact has SHA-256
`6937caa71e2407e9023bbdddbbb7dc22d061569ddbbf1e4f1284897e3c243293`.
Coverage includes generated migration code, using the same filters and thresholds as CI: 9,301 of
10,085 coverable lines and 708 of 1,313 branches. The ReportGenerator warning about unavailable
generated OpenAPI source is not a test failure; no additional files were excluded in response.
These figures describe this source and run.

## Review and regression evidence

The author self-review examined:

- **Privilege escalation:** administrators cannot grant Owner/Administrator or modify privileged
  members, including themselves. Contributors/viewers cannot mutate membership. Numeric role
  ordinals are rejected in invitations and role changes.
- **Last-owner write skew:** deterministic concurrent self-demotions, mutual demotions, and mutual
  deactivations retain an active owner and commit only one audit event. Inactive owners do not count.
- **Stale authority:** waiting invitations, role changes, and deactivations fail after their
  administrator is demoted or deactivated. Current workspace suspension is checked even if the
  request context still says Active. Existing tokens use current permissions on subsequent requests.
- **Stale versions:** conflicting requests accept only one expected version; a separate two-context
  test proves PostgreSQL/EF also rejects stale tracked writes.
- **Tenant isolation:** foreign/missing target responses match, other workspace membership remains
  valid, and a lock in one workspace does not block a membership change in another.
- **Atomicity and cancellation:** an injected error after database SaveChanges still rolls back
  both membership and audit via the outer transaction. Cancellation leaves state unchanged and
  does not prevent a subsequent operation.
- **No-op and input behavior:** current-version no-ops do not create audit/version churn; stale
  retries conflict; missing, malformed, whitespace-padded, and Unicode-normalized versions fail.
- **Data preservation:** deactivation retains history and assignments while rejecting new inactive
  assignments. Audit metadata contains only the allowlisted role fields, not submitted identity data.

The first audit assertion compared JSON whitespace and failed because PostgreSQL `jsonb`
canonicalizes spacing. It was corrected to assert the exact field set and values. The final full
suite above passed without suppressing assertions, skipping cases, or lowering verification gates.

## Migration compatibility

An isolated database was migrated to `20260911162012_WorkItemListingIndexes`, populated with a
workspace/member, upgraded to `20260912132453_MembershipConcurrency`, downgraded, then upgraded again.
Each step retained the member, role, active flag, and existing system version. A subsequent write
advanced the version, and the model had no pending changes.

The generated forward SQL contains only the migration-history insert inside a transaction: Npgsql
does not physically add/drop PostgreSQL's implicit `xmin` column. Downgrading does not undo role
changes or reactivate users; these are business actions, not schema changes.

## Reproduction

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --maxcpucount:1 --logger trx \
  --collect:'XPlat Code Coverage' --results-directory artifacts/member-management-final-verification \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude='[WorkOps.*Tests]*'
dotnet tool run reportgenerator -- \
  '-reports:artifacts/member-management-final-verification/**/coverage.cobertura.xml' \
  '-targetdir:artifacts/member-management-final-coverage' \
  '-assemblyfilters:+WorkOps.*;-WorkOps.*Tests;-* *' \
  '-classfilters:-Microsoft.AspNetCore.OpenApi.Generated.*;-System.Runtime.CompilerServices.*' \
  '-reporttypes:Cobertura;TextSummary;MarkdownSummaryGithub'
scripts/check-coverage.sh artifacts/member-management-final-coverage/Cobertura.xml 70 35
dotnet list package --vulnerable --include-transitive
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project src/WorkOps.Infrastructure --configuration Release --no-build
scripts/check-compose-security.sh
```

Generate coverage only after all four test projects finish, using a dedicated result directory.

## Boundaries

This is a rigorous author self-review with automated regression evidence, not independent approval,
a penetration test, Windows validation, or a throughput/production-readiness claim. Normal requests
already in flight and queued notifications are not globally cancelled on deactivation. Future
membership writers and operator SQL must preserve the common lock protocol and active-owner
invariant. No branch protection, workflow, dependency, release, or deployment changes are included.
