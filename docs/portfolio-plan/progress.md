# Portfolio roadmap progress

This page tracks public portfolio work at an epic level. An item is complete only when its acceptance criteria and evidence are available; repository configuration alone is not sufficient.

| Epic | Status | Current boundary / next proof |
| --- | --- | --- |
| WO-00 Baseline truth boundary | Complete | [Issue #3](https://github.com/adnanjalloh/WorkOps.Platform/issues/3) and [merged PR #5](https://github.com/adnanjalloh/WorkOps.Platform/pull/5) record the 2026-08-04 baseline. |
| WO-01 Governance and reviewer experience | Verified foundation | [Issue #4](https://github.com/adnanjalloh/WorkOps.Platform/issues/4) and [merged PR #6](https://github.com/adnanjalloh/WorkOps.Platform/pull/6) provide reviewer paths, case study, evidence tracking, and templates. [Issue #11](https://github.com/adnanjalloh/WorkOps.Platform/issues/11) synchronizes the settings checklist with verified live controls. |
| WO-02 Release provenance | Verified release | [v0.1.0](https://github.com/adnanjalloh/WorkOps.Platform/releases/tag/v0.1.0) publishes a public GHCR image, SPDX SBOM, immutable digest evidence, and verified build-provenance plus SBOM attestations. [Evidence](../evidence.md#verified-v010-release-evidence) remains digest- and run-specific. |
| WO-03 Reviewer bootstrap | Verified reviewer path | [Issue #12](https://github.com/adnanjalloh/WorkOps.Platform/issues/12) and [PR #24](https://github.com/adnanjalloh/WorkOps.Platform/pull/24) add non-installing Bash/PowerShell validation, safe cleanup, troubleshooting, and screened clean-host evidence. A dev-container path is not advertised because the complete nested Compose scenario has not been proven there. |
| WO-04 Local observability stack | Not started | [Issue #13](https://github.com/adnanjalloh/WorkOps.Platform/issues/13) tracks the collector, storage backends, dashboards, and smoke evidence. |
| WO-05 Durable object storage | Not started | [Issue #14](https://github.com/adnanjalloh/WorkOps.Platform/issues/14) tracks a provider adapter, emulator-backed tests, cleanup, and recovery evidence. |
| WO-06 Malware scanner adapter | Not started | [Issue #15](https://github.com/adnanjalloh/WorkOps.Platform/issues/15) tracks a monitored scanner integration and fail-closed failure-path tests. |
| WO-07 Member lifecycle | Not started | [Issue #16](https://github.com/adnanjalloh/WorkOps.Platform/issues/16) tracks role changes, deactivation/reactivation, concurrency, and final-owner protection. |
| WO-08 Work-item list and search | Not started | [Issue #17](https://github.com/adnanjalloh/WorkOps.Platform/issues/17) tracks tenant-safe listing, filters, pagination, indexes, and query evidence. |
| WO-09 Recovery and chaos evidence | Partial foundation | [Issue #18](https://github.com/adnanjalloh/WorkOps.Platform/issues/18) tracks real-container broker, lease, duplicate, DLQ, replay, and dependency-recovery scenarios. |
| WO-10 Local performance evidence | Partial foundation | [Issue #19](https://github.com/adnanjalloh/WorkOps.Platform/issues/19) tracks reproducible data, percentiles, environment capture, and measured local evidence. |
| WO-11 OpenAPI and contract gate | Partial foundation | [Issue #20](https://github.com/adnanjalloh/WorkOps.Platform/issues/20) tracks a deterministic artifact, error catalog, version policy, and compatibility gate. |
| WO-12 Optional cloud reference | Gated | [Issue #21](https://github.com/adnanjalloh/WorkOps.Platform/issues/21) records the provider, budget, monitoring, expiry, and teardown decision gate; no deployment is approved. |
| WO-13 Case-study site and tour | Partial assets | [Issue #22](https://github.com/adnanjalloh/WorkOps.Platform/issues/22) tracks the static site, genuine captioned tour, privacy-safe CV link, and profile synchronization. |

## Approval gates

Explicit approval is required before:

- merging to the default branch;
- creating or deleting tags and releases;
- publishing packages or container images;
- changing repository visibility or security settings;
- creating paid or externally hosted resources;
- publishing credentials, private data, employer material, or unsupported claims.

## Working rules

- Open a real issue before every non-trivial enhancement.
- Use focused branches and draft pull requests.
- Keep dependencies locked and workflow actions pinned.
- Preserve tenant, authorization, sanitization, and safe-diagnostics controls.
- Attach dated, commit-specific evidence.
- Never manufacture approvals, activity, scan results, usage, or performance claims.
