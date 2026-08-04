# Portfolio roadmap progress

This page tracks public portfolio work at an epic level. An item is complete only when its acceptance criteria and evidence are available; repository configuration alone is not sufficient.

| Epic | Status | Current boundary / next proof |
| --- | --- | --- |
| WO-00 Baseline truth boundary | Draft PR | [Issue #3](https://github.com/adnanjalloh/WorkOps.Platform/issues/3) and [draft PR #5](https://github.com/adnanjalloh/WorkOps.Platform/pull/5) record the 2026-08-04 baseline. Merge requires approval. |
| WO-01 Governance and reviewer experience | In progress | [Issue #4](https://github.com/adnanjalloh/WorkOps.Platform/issues/4) scopes reviewer paths, case study, evidence index, progress tracking, and a security-improvement template. |
| WO-02 Release provenance | Partial, gated | Verification, image, SBOM, and digest paths exist. Public tag, release, package, and attestations require separate approval and generated evidence. |
| WO-03 Reviewer bootstrap | Partial | Bash and PowerShell golden scenarios exist. A tested dedicated bootstrap/dev-container path remains future work. |
| WO-04 Local observability stack | Not started | Instrumentation exists; a collector, storage backends, dashboards, and smoke evidence do not. |
| WO-05 Durable object storage | Not started | Tenant-separated local storage exists; a durable provider adapter and recovery evidence do not. |
| WO-06 Malware scanner adapter | Not started | Fail-closed ports and development behavior exist; a monitored scanner integration does not. |
| WO-07 Member lifecycle | Not started | Invite/list behavior exists; role changes, deactivation/reactivation, and final-owner protection remain. |
| WO-08 Work-item list and search | Not started | Single-item workflows exist; list/search/filter/pagination remain. |
| WO-09 Recovery and chaos evidence | Partial foundation | Retry, failure, replay, duplicate suppression, and metrics exist; real-container recovery scenarios remain. |
| WO-10 Local performance evidence | Partial foundation | A bounded smoke script exists; reproducible data, percentiles, environment capture, and comparison evidence remain. |
| WO-11 OpenAPI and contract gate | Partial foundation | Development OpenAPI exists; a reviewed artifact and compatibility gate remain. |
| WO-12 Optional cloud reference | Gated | No provider, budget, monitoring, or teardown decision has been approved. |
| WO-13 Case-study site and tour | Partial assets | Social-preview and illustrated demo assets exist; no hosted case-study site or genuine captioned recording is claimed. |

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
