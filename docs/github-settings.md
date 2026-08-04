# GitHub repository settings

These controls live in GitHub and cannot be enforced by committed source alone. Verify this
checklist before each release and after material repository-settings changes.

## Repository

- [x] Set `master` as the default branch. Verified through the repository API on 2026-08-04.
- [x] Enable private vulnerability reporting. Verified through the repository API on 2026-08-04.
- [ ] Verify the security policy, Dependabot alerts and updates, secret scanning, and push
  protection where the account plan exposes them.
- [ ] Restrict workflow actions to required publishers and require full commit-SHA pins where the
  organization policy supports it.
- [ ] Disable unused merge methods and automatically delete merged branches.
- [ ] Apply the exact description and topics from [repository metadata](repository-metadata.md).
- [x] Upload `docs/assets/workops-social-preview.png` as the social preview. Visually verified in
  repository settings on 2026-08-04.

## `master` ruleset

- [ ] Require a pull request with **zero required approvals** while this is a solo-maintainer
  repository; document self-review in the pull-request description and never manufacture approval.
- [ ] Increase required approvals and enable stale/latest-push approval rules only after a genuine
  trusted reviewer is consistently available.
- [ ] Require conversation resolution and block force pushes and branch deletion.
- [ ] Require linear history and prevent bypass except for a documented emergency maintainer path.
- [ ] After the first hosted branch and validation-PR runs, select the actual displayed CI, CodeQL,
  and dependency-review check names in the ruleset; do not guess or hard-code unseen contexts. Keep
  the scheduled/manual full-stack demo outside the pull-request gate unless its runtime is acceptable.
- [ ] Require the branch to be current before merge.

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
