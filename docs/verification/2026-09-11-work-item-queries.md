# Work-item queries and messaging recovery — 11 September 2026

## Source and environment

Local verification covers application/test sources at commit
`d3d306d63628c1136067c5cac2b220ecefaf5425`, before this documentation-only evidence update.
The host was macOS arm64 with the pinned .NET SDK `10.0.400` and Docker `29.4.3`.
This report supplements, rather than rewrites, the earlier [dependency repair report](2026-09-11.md).

## Results

| Check | Result |
| --- | --- |
| Locked solution restore | Passed; no dependency version or lockfile changes |
| Formatting verification | Passed |
| Release build | Passed; zero warnings and errors |
| Unit cases | 61 passed |
| PostgreSQL/Redis/RabbitMQ/storage integration cases | 25 passed |
| Full-host functional cases | 40 passed |
| Architecture cases | 8 passed |
| Total | 134 passed; zero failures or skips |
| Coverage floors | Passed; 70% line / 35% branch floors unchanged |
| NuGet audit including transitives | No known vulnerable packages reported by the configured source |
| EF model consistency | No pending model changes |
| Migration SQL review | Forward adds two indexes; rollback drops those two indexes |

ReportGenerator `5.5.11` reports 91.3% line coverage (8,428/9,227), 51.3% branch coverage
(648/1,261), and 89.5% method coverage (663/740). The gate independently rounds the branch rate
to 51.4%. Coverage includes generated migration/snapshot code, so the percentage change is not
solely attributable to deeper testing. The targeted behavioral regressions are the stronger proof.

## New behavioral evidence

- Nineteen HTTP cases cover default and bounded pagination, stable ordering with tied timestamps,
  individual and combined filters, literal backslashes, empty pages/results, read roles, missing
  authentication/context, foreign rows/counts, and invalid query values.
- Eight unit cases cover processor cancellation, empty queues, exhausted retries, completion-save
  and retry-save failures, and clearing uncommitted completion timestamps in pending/failed states.
- One integration case uses a virtual clock and real PostgreSQL. It abandons a committed lease,
  verifies it cannot be reclaimed before expiry, persists the consumer's notification, injects a
  completion-save failure, verifies the retry delay, and redelivers the original message ID. The
  result is one durable inbox receipt and one notification, followed by a processed outbox row.

The integration case initially failed because a pending row retained the timestamp set before a
failed completion save. Clearing that timestamp during failure handling fixed the regression;
the complete suite then passed.

## Reproduction

Use the pinned SDK and a running Docker daemon:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --maxcpucount:1 --logger trx \
  --collect:"XPlat Code Coverage" --results-directory artifacts/work-item-query-verification \
  -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[WorkOps.*Tests]*"
dotnet ef migrations has-pending-model-changes --project src/WorkOps.Infrastructure \
  --startup-project src/WorkOps.Infrastructure --configuration Release --no-build
dotnet list package --vulnerable --include-transitive
```

Use the [coverage merge commands](../testing.md#commands) with the result directory above. A fresh
result directory avoids mixing different source revisions. The integration/functional fixtures
apply the migrations to disposable PostgreSQL databases; rollback SQL was reviewed, not applied
to a deployed database.

## Boundaries

These are local results. Required hosted CI, CodeQL, dependency review, and the full-stack scenario
are tracked separately on [PR #65](https://github.com/adnanjalloh/WorkOps.Platform/pull/65).
The new recovery case uses a deterministic publisher with a real database-backed consumer; it
does not simulate RabbitMQ outages, network partitions, or overlapping live workers after lease
expiry. No new release, hosted production deployment, benchmark, or production-readiness claim
is made. See the [query contract](../work-item-queries.md) for pagination and migration limits.
