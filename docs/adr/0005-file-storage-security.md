# ADR 0005: File storage security

- Status: Accepted for the portfolio milestone
- Date: 2026-08-01

## Context

Work-item attachments cross several trust boundaries: multipart HTTP input, application memory, a
malware-scanning provider, private storage, metadata persistence, and an authorized download. A
client-controlled name, media type, or tenant identifier cannot safely choose a storage location or
decide whether content is acceptable.

## Decision

Version 1 accepts only PDF, PNG, and plain UTF-8 text files up to 512 KiB. The application sanitizes
the display filename and media type, requires the allowlisted extension, media type, and signature
to agree, rejects invalid/control-heavy text, and scans bytes before storage. Archives and active
document formats are unsupported.

Storage receives only the established workspace identifier and an opaque GUID-derived name. The
local adapter writes below an absolute root outside the web root, in a per-workspace directory, with
create-only semantics. Attachment metadata has relational ownership constraints and a global tenant
filter. Downloads resolve that tenant-filtered metadata before opening the file and return
`X-Content-Type-Options: nosniff` with a safe attachment filename.

Storage precedes the metadata transaction because the current local adapter has no distributed
transaction. A metadata failure triggers best-effort deletion. Orphan detection and reconciliation
are required for a durable remote adapter.

The scanner fails closed by default. The included clean result adapter is deliberately limited to
local demonstration and automated tests; it is not malware protection.

## Consequences

- Client paths and filenames never determine the physical path.
- Tenant authorization occurs before a storage lookup, reducing identifier disclosure and file
  leakage risk.
- Buffering is acceptable at the deliberately small limit and makes signature scanning deterministic.
- Local Compose storage is ephemeral. Production requires durable private object storage,
  encryption and retention policy, backup/restore evidence, a monitored antivirus service, and
  orphan reconciliation.
- Adding another file type requires a new signature policy and regression tests; changing only the
  extension allowlist is insufficient.

## Evidence

- `AttachmentPolicyTests` covers accepted formats, MIME/extension mismatches, signature mismatch,
  invalid UTF-8, empty files, and the size limit.
- Integration tests prove tenant-separated local storage paths.
- Functional tests prove exact authorized download, `nosniff`, path-traversal rejection,
  MIME/signature mismatch rejection, oversize rejection, and cross-workspace denial.
