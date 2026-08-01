#!/usr/bin/env bash
set -euo pipefail

mode="${1:---report}"
if [[ "$mode" != "--report" && "$mode" != "--delete-orphans" ]]; then
  echo "Usage: $0 [--report|--delete-orphans]" >&2
  exit 2
fi

: "${WORKOPS_FILES_ROOT:?Set WORKOPS_FILES_ROOT to the absolute attachment root.}"
if [[ "$WORKOPS_FILES_ROOT" != /* || ! -d "$WORKOPS_FILES_ROOT" ]]; then
  echo "WORKOPS_FILES_ROOT must be an existing absolute directory." >&2
  exit 2
fi

command -v psql >/dev/null || {
  echo "psql is required; configure it with standard PGHOST/PGPORT/PGDATABASE/PGUSER credentials." >&2
  exit 2
}

task_tmp=$(mktemp -d)
trap 'rm -rf "$task_tmp"' EXIT
database_paths="$task_tmp/database-paths.txt"
storage_paths="$task_tmp/storage-paths.txt"
missing_paths="$task_tmp/missing-paths.txt"
orphan_paths="$task_tmp/orphan-paths.txt"

psql --no-password --tuples-only --no-align --set ON_ERROR_STOP=1 \
  --command 'SELECT replace("WorkspaceId"::text, '\''-'\'', '\'''\'') || '\''/'\'' || "StorageName" FROM attachments ORDER BY 1' \
  > "$database_paths"

while IFS= read -r storage_path; do
  relative_path="${storage_path#"$WORKOPS_FILES_ROOT"/}"
  if [[ "$relative_path" =~ ^[0-9a-f]{32}/[0-9a-f]{32}\.bin$ ]]; then
    printf '%s\n' "$relative_path"
  fi
done < <(find "$WORKOPS_FILES_ROOT" -type f -name '*.bin' -print) | sort -u > "$storage_paths"

comm -23 "$database_paths" "$storage_paths" > "$missing_paths"
comm -13 "$database_paths" "$storage_paths" > "$orphan_paths"

missing_count=$(wc -l < "$missing_paths" | tr -d ' ')
orphan_count=$(wc -l < "$orphan_paths" | tr -d ' ')
printf 'Missing backing objects: %s\nOrphaned backing objects: %s\n' "$missing_count" "$orphan_count"

if [[ "$missing_count" -gt 0 ]]; then
  echo "Database rows with missing content:"
  sed 's/^/  /' "$missing_paths"
fi

if [[ "$orphan_count" -gt 0 ]]; then
  echo "Storage objects without database rows:"
  sed 's/^/  /' "$orphan_paths"
fi

if [[ "$mode" == "--delete-orphans" ]]; then
  while IFS= read -r relative_path; do
    [[ -n "$relative_path" ]] || continue
    if [[ ! "$relative_path" =~ ^[0-9a-f]{32}/[0-9a-f]{32}\.bin$ ]]; then
      echo "Refusing unexpected storage path: $relative_path" >&2
      exit 1
    fi

    rm -- "$WORKOPS_FILES_ROOT/$relative_path"
  done < "$orphan_paths"
  printf 'Deleted orphaned backing objects: %s\n' "$orphan_count"
fi
