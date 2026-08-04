#!/usr/bin/env bash

set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
api_port=${WORKOPS_HTTP_PORT:-8080}
identity_port=${WORKOPS_IDENTITY_PORT:-8081}
api_url=${WORKOPS_API_URL:-http://localhost:${api_port}}
identity_url=${WORKOPS_IDENTITY_URL:-http://localhost:${identity_port}}
evidence_directory=${WORKOPS_DEMO_EVIDENCE_DIR:-${repo_root}/artifacts/reviewer-demo}
action=validate

usage() {
  cat <<'EOF'
Usage: ./scripts/bootstrap.sh [--cleanup]

With no option, validates the reviewer environment without installing tools or starting services.
--cleanup stops this repository's Compose stack while preserving named volumes.
EOF
}

pass() {
  printf '  [ok] %s\n' "$1"
}

warn() {
  printf '  [warn] %s\n' "$1" >&2
}

fail() {
  printf '  [error] %s\n' "$1" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "$2"
}

validate_port() {
  local port=$1
  local label=$2
  [[ "$port" =~ ^[0-9]+$ ]] && ((port >= 1 && port <= 65535)) \
    || fail "${label} port must be an integer from 1 to 65535."
}

port_is_open() {
  local port=$1
  (exec 3<>"/dev/tcp/127.0.0.1/${port}") >/dev/null 2>&1
}

check_port() {
  local port=$1
  local service=$2
  local label=$3

  if ! port_is_open "$port"; then
    pass "${label} port ${port} is available"
    return
  fi

  if grep -Fxq "$service" <<<"$running_services"; then
    pass "${label} port ${port} is already used by this Compose stack"
    return
  fi

  fail "${label} port ${port} is already in use. Set the matching WORKOPS_*_PORT override or stop the conflicting process."
}

while (($# > 0)); do
  case "$1" in
    --cleanup)
      action=cleanup
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      usage >&2
      fail "Unknown option: $1"
      ;;
  esac
  shift
done

require_command docker "Docker is required. Install and start Docker Desktop or a compatible Docker Engine."

if [[ "$action" == cleanup ]]; then
  printf 'Stopping the local WorkOps stack (named volumes are preserved)\n'
  docker compose --project-directory "$repo_root" down --remove-orphans
  printf '\nCleanup complete\n'
  printf '  API URL:       %s\n' "$api_url"
  printf '  Identity URL:  %s\n' "$identity_url"
  printf '  Data volumes:  preserved\n'
  exit 0
fi

printf 'Validating reviewer prerequisites (no tools will be installed and no services will be started)\n'

validate_port "$api_port" 'API'
validate_port "$identity_port" 'Identity'

for required_file in \
  docker-compose.yml \
  Dockerfile \
  global.json \
  scripts/demo.sh \
  scripts/check-compose-security.sh; do
  [[ -f "$repo_root/$required_file" ]] || fail "Repository prerequisite is missing: $required_file"
done
pass 'repository prerequisites are present'

require_command curl "curl is required by scripts/demo.sh."
require_command jq "jq is required by scripts/demo.sh."
pass 'curl and jq are available'

docker info >/dev/null 2>&1 || fail "Docker is installed but its daemon is unavailable. Start Docker and retry."
docker compose version >/dev/null 2>&1 || fail "The Docker Compose plugin is required (docker compose)."
pass "Docker client and daemon are available ($(docker version --format '{{.Client.Version}}/{{.Server.Version}}'))"
pass "Docker Compose is available ($(docker compose version --short))"

docker compose --project-directory "$repo_root" config --quiet || fail "docker-compose.yml did not validate."
"$repo_root/scripts/check-compose-security.sh" >/dev/null
pass 'Compose configuration and loopback-only host boundary passed'

sdk_version=$(jq -er '.sdk.version | select(type == "string" and test("^[0-9]+\\.[0-9]+\\.[0-9]+$"))' "$repo_root/global.json") \
  || fail "global.json does not contain a valid sdk.version."
grep -Fq "mcr.microsoft.com/dotnet/sdk:${sdk_version}-" "$repo_root/Dockerfile" \
  || fail "Dockerfile build SDK does not match global.json (${sdk_version})."
pass ".NET SDK metadata is consistent (${sdk_version})"

if command -v dotnet >/dev/null 2>&1; then
  if dotnet --list-sdks | awk '{print $1}' | grep -Fxq "$sdk_version"; then
    pass "optional local .NET SDK ${sdk_version} is available"
  else
    warn "Local .NET SDK ${sdk_version} is not installed; the containerized reviewer path remains available."
  fi
else
  warn 'dotnet is not installed; it is optional for the containerized reviewer path.'
fi

running_services=$(docker compose --project-directory "$repo_root" ps --services --status running 2>/dev/null || true)
check_port "$api_port" api 'API'
check_port "$identity_port" identity 'Identity'

docker_memory=$(docker info --format '{{.MemTotal}}' 2>/dev/null || true)
if [[ "$docker_memory" =~ ^[0-9]+$ ]] && ((docker_memory < 4294967296)); then
  warn 'Docker reports less than 4 GiB of memory; the five-service stack may start slowly or fail.'
fi

printf '\nReviewer environment ready\n'
printf '  API URL:          %s\n' "$api_url"
printf '  Identity URL:     %s\n' "$identity_url"
printf '  Scenario status:  not started\n'
printf '  Evidence path:    %s\n' "$evidence_directory"
printf '  Start command:    ./scripts/demo.sh --start\n'
printf '  Cleanup command:  ./scripts/bootstrap.sh --cleanup\n'
printf '  Credentials:      not printed or inspected\n'
