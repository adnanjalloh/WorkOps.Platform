# GitHub repository settings

These controls live in GitHub and cannot be enforced by committed source alone. Apply this checklist
before making the repository public or creating the first release.

## Repository

- [ ] Set `master` as the default branch.
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

- [ ] Create a `release` environment with no untrusted deployment branches. Add a required reviewer
  only when a genuinely independent reviewer is available; otherwise describe the maintainer gate
  accurately.
- [ ] Add a `v*` tag ruleset that blocks updates and deletion and limits tag creation to maintainers.
- [ ] Confirm the workflow token may write repository contents and packages only for the `publish`
  job; provenance permissions must also remain limited to that job, while the `verify` job stays
  read-only and outside the protected environment.
- [ ] Verify the GHCR package is linked to this repository and set its intended visibility.
- [ ] After each release, compare the attached digest with the GHCR image and retain the SPDX SBOM.

The workflow deliberately publishes only on a semantic version tag at the current `master` head.
Its read-only verification job scans and packages the exact candidate before the protected publish
job receives registry and attestation permissions. The configured provenance and SPDX SBOM
attestations must not be claimed as generated evidence until an approved release completes and the
documented verification commands pass.
