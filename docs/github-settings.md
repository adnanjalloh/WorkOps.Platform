# GitHub repository settings

These controls live in GitHub and cannot be enforced by committed source alone. Verify this
checklist before each release and after material repository-settings changes.

## Repository

- [x] Set `master` as the default branch. Verified through the repository API on 2026-08-04.
- [x] Enable private vulnerability reporting. Verified through the repository API on 2026-08-04.
- [x] Keep the security policy, Dependabot alerts and security updates, secret scanning, and push
  protection enabled. Verified from the committed policy and repository API on 2026-08-04.
- [x] Restrict workflow actions to the selected allowlist and require full commit-SHA pins. Verified
  through the repository Actions-permissions API on 2026-08-04.
- [x] Allow squash merging only and automatically delete merged branches. Verified through the
  repository API on 2026-08-04.
- [x] Apply the exact description and topics from [repository metadata](repository-metadata.md).
  Verified through the repository API on 2026-08-04.
- [x] Upload `docs/assets/workops-social-preview.png` as the social preview. Visually verified in
  repository settings on 2026-08-04.

## `master` branch protection

- [x] Require a pull request with **zero required approvals** while this is a solo-maintainer
  repository. Verified through the branch-protection API on 2026-08-04; self-review remains part of
  the pull-request description and no approval is manufactured.
- [ ] Increase required approvals and enable stale/latest-push approval rules only after a genuine
  trusted reviewer is consistently available.
- [x] Require conversation resolution and block force pushes and branch deletion. Verified through
  the branch-protection API on 2026-08-04.
- [x] Require linear history and enforce protection for administrators. Verified through the
  branch-protection API on 2026-08-04; no emergency bypass is configured.
- [x] Require the hosted `verify`, `Analyze C#`, and `review` checks. Verified with validation pull
  requests on 2026-08-04. The scheduled/manual full-stack demo remains outside the pull-request gate.
- [x] Require the branch to be current before merge. Strict status checks were verified through the
  branch-protection API on 2026-08-04.

## Releases

- [x] Create a `release` environment restricted to `v*` tags. Verified on 2026-08-04. No required
  reviewer is configured because no genuinely independent reviewer is currently available.
- [x] Add an active `v*` tag ruleset that restricts creation to repository administrators and blocks
  updates, deletion, and non-fast-forward changes. Verified on 2026-08-04.
- [x] Confirm the workflow token receives contents, packages, OIDC, and attestation write permissions
  only in the protected `publish` job; the `verify` job remains read-only and outside the protected
  environment. Verified in release run `30901202020`.
- [x] Verify the GHCR package is public, linked to this repository, and grants this repository Actions
  access. Verified on 2026-08-04.
- [x] Compare the attached `v0.1.0` digest with both public GHCR tags, retain the SPDX SBOM, and verify
  both attestations. Completed on 2026-08-04.

The workflow deliberately publishes only on a semantic version tag at the current `master` head.
Its read-only verification job scans and packages the exact candidate before the protected publish
job receives registry and attestation permissions. The configured provenance and SPDX SBOM
attestations were generated for `v0.1.0` and independently verified against the public registry
digest. Each later release requires its own generated and verified evidence.
