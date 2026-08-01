#!/usr/bin/env bash

set -euo pipefail

base_url="${WORKOPS_BASE_URL:-http://localhost:8080}"
access_token="${WORKOPS_ACCESS_TOKEN:-}"
workspace_id="${WORKOPS_WORKSPACE_ID:-}"
request_count="${WORKOPS_REQUESTS:-20}"

if [[ -z "$access_token" || -z "$workspace_id" ]]; then
  echo "Set WORKOPS_ACCESS_TOKEN and WORKOPS_WORKSPACE_ID before running this smoke check." >&2
  exit 2
fi

if [[ ! "$request_count" =~ ^[0-9]+$ ]] || ((request_count < 1 || request_count > 200)); then
  echo "WORKOPS_REQUESTS must be an integer from 1 to 200." >&2
  exit 2
fi

result_file="$(mktemp)"
trap 'rm -f "$result_file"' EXIT

curl_with_token() {
  printf 'header = "Authorization: Bearer %s"\n' "$access_token" | curl --config - "$@"
}

for ((request = 1; request <= request_count; request++)); do
  curl_with_token \
    --silent \
    --show-error \
    --output /dev/null \
    --write-out '%{http_code} %{time_total}\n' \
    --header "X-Workspace-Id: $workspace_id" \
    "$base_url/api/v1/features" >>"$result_file"
done

awk '
  { total += $2; if ($2 > maximum) maximum = $2; if ($1 == 200) passed++ }
  END {
    printf "requests=%d successful=%d average_seconds=%.4f maximum_seconds=%.4f\n", NR, passed, total / NR, maximum
    if (passed != NR) exit 1
  }
' "$result_file"

echo "Local smoke result only; this is not a production benchmark."
