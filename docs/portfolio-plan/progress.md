# Portfolio roadmap progress

This page records the portfolio scope delivered through v0.1.0 and the ideas deliberately deferred when that scope was closed. Deferred items are not implementation claims.

| Epic | Status | Current boundary / next proof |
| --- | --- | --- |
| WO-00 Baseline truth boundary | Complete | [Issue #3](https://github.com/adnanjalloh/WorkOps.Platform/issues/3) and [merged PR #5](https://github.com/adnanjalloh/WorkOps.Platform/pull/5) record the 2026-08-04 baseline. |
| WO-01 Governance and reviewer experience | Verified foundation | [Issue #4](https://github.com/adnanjalloh/WorkOps.Platform/issues/4) and [merged PR #6](https://github.com/adnanjalloh/WorkOps.Platform/pull/6) provide reviewer paths, case study, evidence tracking, and templates. [Issue #11](https://github.com/adnanjalloh/WorkOps.Platform/issues/11) synchronizes the settings checklist with verified live controls. |
| WO-02 Release provenance | Verified release | [v0.1.0](https://github.com/adnanjalloh/WorkOps.Platform/releases/tag/v0.1.0) publishes a public GHCR image, SPDX SBOM, immutable digest evidence, and verified build-provenance plus SBOM attestations. [Evidence](../evidence.md#verified-v010-release-evidence) remains digest- and run-specific. |
| WO-03 Reviewer bootstrap | Verified reviewer path | [Issue #12](https://github.com/adnanjalloh/WorkOps.Platform/issues/12) and [PR #24](https://github.com/adnanjalloh/WorkOps.Platform/pull/24) add non-installing Bash/PowerShell validation, safe cleanup, troubleshooting, and screened clean-host evidence. A dev-container path is not advertised because the complete nested Compose scenario has not been proven there. |
| WO-04 Local observability stack | Deferred | OpenTelemetry remains opt-in; no local dashboard stack or production-monitoring claim is included. |
| WO-05 Durable object storage | Deferred | The tenant-separated local storage adapter remains an explicit durability boundary. |
| WO-06 Malware scanner adapter | Deferred | The development scanner remains a stub, while non-development configuration continues to fail closed. |
| WO-07 Member lifecycle | Deferred | Invitation and membership listing remain the supported scope; lifecycle administration is not claimed. |
| WO-08 Work-item list and search | Deferred | Command and single-item behavior remain the supported scope; list and search are not claimed. |
| WO-09 Recovery and chaos evidence | Deferred | Recovery foundations remain documented, without claiming a completed chaos test suite. |
| WO-10 Local performance evidence | Deferred | The bounded smoke script remains available, without production-scale or benchmark claims. |
| WO-11 OpenAPI and contract gate | Deferred | Development OpenAPI remains available; no deterministic compatibility gate is claimed. |
| WO-12 Optional cloud reference | Closed as reference-only | No cloud deployment, budget, monitoring ownership, or hosted-service claim is approved. |
| WO-13 Case-study site and tour | Closed at repository scope | The case study, reviewer guide, release evidence, and two-minute tour are hosted in this repository; a separate site, public CV, and profile automation are not claimed. |

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
