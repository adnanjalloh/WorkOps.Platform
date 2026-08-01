# Changelog

All notable changes to this project will be documented here. The project follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and will use semantic versioning after the
first release.

## [Unreleased]

### Added

- Clean-room .NET 10 modular-monolith foundation.
- Health endpoints and initial functional test.
- Unit, integration-smoke, functional, and architecture test projects.
- Pinned container baseline and GitHub security/delivery automation.
- Initial architecture, security, testing, demo, operations, and threat-model documentation.
- PostgreSQL workspace, user, membership, and migration model.
- Strict JWT authentication and role-based permission policies.
- Verified request-scoped workspace context with default-deny tenant query filters.
- Sanitization profiles with automated request-surface coverage.
- Container-backed tenant-isolation and HTTP security regression tests.
- Tenant-safe project creation, lookup, filtering, pagination, and archiving.
- Contributor and viewer invitations with role and membership boundaries.
- Assigned, labeled work-item creation and updates with an explicit transition state machine.
- Opaque PostgreSQL `xmin` concurrency tokens with stale writes returned as `409 Conflict`.
- Functional coverage for the initial project and work-item golden scenario.
