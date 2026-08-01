#!/usr/bin/env bash

set -euo pipefail

evidence_file=${1:?Usage: check-demo-evidence.sh <evidence-file>}

if [[ ! -f "$evidence_file" ]]; then
  echo "Evidence file not found." >&2
  exit 2
fi

credential_pattern='([Aa]uthorization:[[:space:]]*[Bb]earer|"(access_token|refresh_token)"[[:space:]]*:|[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,})'
if LC_ALL=C grep -Eq "$credential_pattern" "$evidence_file"; then
  printf '%s\n' 'Evidence withheld: potential credential-like content detected.' > "$evidence_file"
  echo "Potential credential-like content was blocked from the evidence artifact." >&2
  exit 1
fi

echo "Synthetic demo evidence credential screen: PASS"
