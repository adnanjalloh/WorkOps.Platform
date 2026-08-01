# GitHub repository settings

These controls live in GitHub and cannot be enforced by committed source alone. Apply this checklist
before making the repository public or creating the first release.

## Repository

- [ ] Set `master` as the default branch.
- [ ] Enable private vulnerability reporting, the security policy, Dependabot alerts and updates,
  secret scanning, and push protection.
- [ ] Restrict workflow actions to required publishers and require full commit-SHA pins where the
  organization policy supports it.
- [ ] Disable unused merge methods and automatically delete merged branches.

## `master` ruleset

- [ ] Require a pull request with at least one approval.
- [ ] Dismiss stale approvals and require approval of the latest reviewable push.
- [ ] Require conversation resolution and block force pushes and branch deletion.
- [ ] Require linear history and prevent bypass except for a documented emergency maintainer path.
- [ ] Require `CI / verify`, `CodeQL / Analyze C#`, and `Dependency review / review` checks to pass.
- [ ] Require the branch to be current before merge.

## Releases

- [ ] Create a `release` environment with a required reviewer and no untrusted deployment branches.
- [ ] Add a `v*` tag ruleset that blocks updates and deletion and limits tag creation to maintainers.
- [ ] Confirm the workflow token may write repository contents and packages only for the release job.
- [ ] Verify the GHCR package is linked to this repository and set its intended visibility.
- [ ] After each release, compare the attached digest with the GHCR image and retain the SPDX SBOM.

The workflow deliberately publishes only on a semantic version tag, verifies the tag is on
`master`, scans before registry login, and also publishes a commit-addressed tag. Signing and
provenance attestation remain future hardening work and must not be claimed until implemented.
