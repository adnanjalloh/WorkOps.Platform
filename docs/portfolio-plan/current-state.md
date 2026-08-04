# Portfolio current state

This document records generated local evidence for the repository state before portfolio enhancement work begins. It is intentionally dated and commit-specific; it is not a general production-readiness claim.

- **Verified (UTC):** 2026-08-04
- **Issue:** [#3 — WO-00 baseline evidence](https://github.com/adnanjalloh/WorkOps.Platform/issues/3)
- **Branch verified:** `master`
- **Commit verified:** `7e15cf45eaa6c3b23ffafec078cf53f8b1d8cb01`
- **Host:** macOS 26.6, Apple arm64
- **.NET SDK:** 10.0.302 (repository-pinned, workspace-local installation)
- **.NET runtime:** 10.0.10
- **Docker client/server:** 29.4.3 / 29.4.3

## Baseline results

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Repository tool restore | Passed |
| Formatting verification | Passed |
| Release build | Passed — 0 warnings, 0 errors |
| Architecture tests | Passed — 8 |
| Functional tests | Passed — 21 |
| Integration tests | Passed — 24 |
| Unit tests | Passed — 53 |
| Total tests | Passed — 106 |
| Line coverage | 90.3% — 7,653 of 8,466 coverable lines |
| Branch coverage | 48.9% — 599 of 1,223 branches |
| Configured coverage gate | Passed — 70% lines / 35% branches |
| Compose host-port boundary | Passed |
| Compose configuration | Passed |
| Containerized golden scenario | Passed |

The coverage gate prints one-decimal percentages calculated from the exact Cobertura rates. ReportGenerator's summary is the source for the repository's dated `90.3%` line and `48.9%` branch figures.

## Commands executed

```bash
dotnet restore --locked-mode
dotnet tool restore
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --maxcpucount:1 --logger "trx" \
  --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet tool run reportgenerator -- \
  "-reports:artifacts/test-results/**/coverage.cobertura.xml" \
  "-targetdir:artifacts/coverage" \
  "-assemblyfilters:+WorkOps.*;-WorkOps.*Tests;-* *" \
  "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated.*;-System.Runtime.CompilerServices.*" \
  "-reporttypes:Cobertura;TextSummary"
./scripts/check-coverage.sh artifacts/coverage/Cobertura.xml 70 35
./scripts/check-compose-security.sh
docker compose config --quiet
./scripts/demo.sh --start
docker compose down
```

## Golden-scenario evidence

The existing synthetic demo completed successfully and verified:

- owner, contributor, viewer, and outsider authentication;
- two isolated workspaces and role-scoped memberships;
- idempotent project creation with exact replay;
- viewer write denial with `403 Forbidden`;
- work-item creation, update, and state transition;
- stale-write rejection with `409 Conflict`;
- cross-workspace reads returning a non-disclosing `404`;
- transactional audit evidence;
- outbox-backed notification delivery.

The script did not intentionally print or persist access tokens. Synthetic workspace, project, and work-item identifiers are intentionally omitted because they are run-specific and are not required to reproduce the result.

## Evidence boundary

This verification shows that the recorded commit built, passed its configured test and coverage gates, passed Compose validation, and completed the documented local golden scenario in the environment above.

It does **not** establish:

- production deployment or production users;
- cloud or Kubernetes deployment;
- penetration testing or security certification;
- production-scale performance, availability, or durability;
- production suitability of the documented local identity, file-storage, or scanner adapters.

Future evidence must record its own date, commit, environment, commands, and limitations instead of silently replacing this result.
