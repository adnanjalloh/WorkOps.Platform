#!/usr/bin/env bash

set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
api_url=${WORKOPS_API_URL:-http://localhost:8080}
identity_url=${WORKOPS_IDENTITY_URL:-http://localhost:8081}
demo_password=${WORKOPS_DEMO_PASSWORD:-local-demo-only}
state_file=${WORKOPS_DEMO_STATE:-$repo_root/.local/demo-state.json}
demo_temp_dir=$(mktemp -d "${TMPDIR:-/tmp}/workops-demo.XXXXXX")
trap 'rm -rf "$demo_temp_dir"' EXIT

HTTP_STATUS=""
HTTP_BODY=""
HTTP_HEADERS=""

step() {
  printf '\n==> %s\n' "$1"
}

pass() {
  printf '  [ok] %s\n' "$1"
}

fail() {
  printf '  [failed] %s\n' "$1" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || fail "Required command not found: $1"
}

curl_with_token() {
  local token=$1
  shift
  if [[ -n "$token" ]]; then
    printf 'header = "Authorization: Bearer %s"\n' "$token" | curl --config - "$@"
    return
  fi

  curl "$@"
}

http_json() {
  local method=$1
  local path=$2
  local token=${3:-}
  local workspace_id=${4:-}
  local payload=${5:-}
  local idempotency_key=${6:-}
  local body_file="$demo_temp_dir/body.json"
  local header_file="$demo_temp_dir/headers.txt"
  local -a args=(
    --silent
    --show-error
    --request "$method"
    --output "$body_file"
    --dump-header "$header_file"
    --write-out '%{http_code}'
  )

  [[ -n "$workspace_id" ]] && args+=(--header "X-Workspace-Id: $workspace_id")
  [[ -n "$idempotency_key" ]] && args+=(--header "Idempotency-Key: $idempotency_key")
  if [[ -n "$payload" ]]; then
    args+=(--header 'Content-Type: application/json' --data "$payload")
  fi

  HTTP_STATUS=$(curl_with_token "$token" "${args[@]}" "$api_url$path")
  HTTP_BODY=$(<"$body_file")
  HTTP_HEADERS=$(<"$header_file")
}

expect_status() {
  local expected=$1
  local label=$2
  if [[ "$HTTP_STATUS" != "$expected" ]]; then
    printf 'Expected HTTP %s but received %s for %s.\n' "$expected" "$HTTP_STATUS" "$label" >&2
    jq . <<< "$HTTP_BODY" 2>/dev/null || printf '%s\n' "$HTTP_BODY" >&2
    exit 1
  fi
}

expect_problem() {
  local expected_code=$1
  local actual_code
  actual_code=$(jq -r '.code // empty' <<< "$HTTP_BODY")
  [[ "$actual_code" == "$expected_code" ]] || fail "Expected problem code $expected_code, received ${actual_code:-none}"
}

token_for() {
  local username=$1
  local response
  response=$(curl --silent --show-error --fail-with-body \
    --request POST \
    --header 'Content-Type: application/x-www-form-urlencoded' \
    --data-urlencode 'client_id=workops-cli' \
    --data-urlencode 'grant_type=password' \
    --data-urlencode 'scope=openid profile email' \
    --data-urlencode "username=$username" \
    --data-urlencode "password=$demo_password" \
    "$identity_url/realms/workops/protocol/openid-connect/token")
  jq -er '.access_token' <<< "$response"
}

subject_for() {
  local token=$1
  curl_with_token "$token" --silent --show-error --fail-with-body \
    "$identity_url/realms/workops/protocol/openid-connect/userinfo" | jq -er '.sub'
}

wait_for_url() {
  local url=$1
  local label=$2
  local attempts=${3:-120}
  local index
  for ((index = 1; index <= attempts; index++)); do
    if curl --silent --fail --output /dev/null "$url"; then
      pass "$label is ready"
      return
    fi
    sleep 1
  done
  fail "$label did not become ready"
}

show_summary() {
  local workspace_id=$1
  local project_id=$2
  local work_item_id=$3
  printf '\nGolden scenario complete\n'
  printf '  Workspace: %s\n' "$workspace_id"
  printf '  Project:   %s\n' "$project_id"
  printf '  Work item: %s\n' "$work_item_id"
  printf '  Evidence:  authorization, tenant isolation, concurrency, audit, outbox notification\n'
  printf '  Tokens:    not intentionally printed or persisted by this script\n'
}

require_command curl
require_command jq

if [[ ${1:-} == "--start" ]]; then
  require_command docker
  step "Starting the local stack"
  docker compose --project-directory "$repo_root" up --detach --build
fi

step "Waiting for local services"
wait_for_url "$identity_url/realms/workops/.well-known/openid-configuration" "Identity provider"
wait_for_url "$api_url/health/ready" "WorkOps API"

step "Obtaining synthetic user tokens"
owner_token=$(token_for demo-owner)
contributor_token=$(token_for demo-contributor)
viewer_token=$(token_for demo-viewer)
outsider_token=$(token_for demo-outsider)
contributor_subject=$(subject_for "$contributor_token")
viewer_subject=$(subject_for "$viewer_token")
pass "owner, contributor, viewer, and outsider authenticated"

if [[ -f "$state_file" ]]; then
  workspace_id=$(jq -r '.workspaceId // empty' "$state_file")
  outsider_workspace_id=$(jq -r '.outsiderWorkspaceId // empty' "$state_file")
  project_id=$(jq -r '.projectId // empty' "$state_file")
  work_item_id=$(jq -r '.workItemId // empty' "$state_file")
  stale_version=$(jq -r '.staleVersion // empty' "$state_file")

  if [[ -n "$workspace_id" && -n "$work_item_id" ]]; then
    http_json GET "/api/v1/work-items/$work_item_id" "$contributor_token" "$workspace_id"
    if [[ "$HTTP_STATUS" == "200" ]]; then
      step "Reusing the saved idempotent demo state"
      pass "existing work item is visible to its contributor"

      stale_payload=$(jq -cn --arg status Blocked --arg version "$stale_version" \
        '{targetStatus:$status,expectedVersion:$version}')
      http_json POST "/api/v1/work-items/$work_item_id/transitions" \
        "$contributor_token" "$workspace_id" "$stale_payload"
      expect_status 409 "stale transition"
      expect_problem concurrency_conflict
      pass "stale transition remains a safe 409 Conflict"

      http_json GET "/api/v1/work-items/$work_item_id" "$outsider_token" "$outsider_workspace_id"
      expect_status 404 "cross-workspace read"
      pass "outsider still receives a non-disclosing 404"

      show_summary "$workspace_id" "$project_id" "$work_item_id"
      exit 0
    fi
  fi
fi

run_id=$(date -u +%Y%m%d%H%M%S)

step "Creating two isolated workspaces"
owner_workspace_payload=$(jq -cn --arg name "WorkOps Demo" --arg slug "workops-demo-$run_id" \
  '{name:$name,slug:$slug}')
http_json POST /api/v1/workspaces/ "$owner_token" "" "$owner_workspace_payload"
expect_status 201 "owner workspace creation"
workspace_id=$(jq -er '.id' <<< "$HTTP_BODY")

outsider_workspace_payload=$(jq -cn --arg name "Outsider Demo" --arg slug "outsider-demo-$run_id" \
  '{name:$name,slug:$slug}')
http_json POST /api/v1/workspaces/ "$outsider_token" "" "$outsider_workspace_payload"
expect_status 201 "outsider workspace creation"
outsider_workspace_id=$(jq -er '.id' <<< "$HTTP_BODY")
pass "workspace ownership boundaries established"

step "Inviting contributor and viewer"
contributor_invite=$(jq -cn --arg subject "$contributor_subject" --arg name "Demo Contributor" \
  --arg role ProjectContributor '{subject:$subject,displayName:$name,role:$role}')
http_json POST "/api/v1/workspaces/$workspace_id/invitations" \
  "$owner_token" "" "$contributor_invite"
expect_status 201 "contributor invitation"
contributor_user_id=$(jq -er '.userId' <<< "$HTTP_BODY")

viewer_invite=$(jq -cn --arg subject "$viewer_subject" --arg name "Demo Viewer" \
  --arg role Viewer '{subject:$subject,displayName:$name,role:$role}')
http_json POST "/api/v1/workspaces/$workspace_id/invitations" "$owner_token" "" "$viewer_invite"
expect_status 201 "viewer invitation"
pass "role-scoped memberships created"

step "Creating an idempotent project"
project_payload=$(jq -cn --arg name "Delivery Platform" --arg key "demo-$run_id" \
  '{name:$name,key:$key}')
http_json POST /api/v1/projects/ "$owner_token" "$workspace_id" "$project_payload" "demo-project-$run_id"
expect_status 201 "project creation"
project_id=$(jq -er '.id' <<< "$HTTP_BODY")

http_json POST /api/v1/projects/ "$owner_token" "$workspace_id" "$project_payload" "demo-project-$run_id"
expect_status 201 "project replay"
grep -qi '^Idempotency-Replayed: true' <<< "$HTTP_HEADERS" || fail "Project replay header was not returned"
pass "exact replay returned the original project"

step "Checking viewer write denial"
viewer_project_payload=$(jq -cn --arg name "Forbidden Project" --arg key "forbidden-$run_id" \
  '{name:$name,key:$key}')
http_json POST /api/v1/projects/ "$viewer_token" "$workspace_id" "$viewer_project_payload"
expect_status 403 "viewer project creation"
pass "viewer write returned 403 Forbidden"

step "Creating, updating, and transitioning a work item"
work_item_payload=$(jq -cn --arg title "Deliver tenant-safe workflow" --arg priority High \
  --arg assignee "$contributor_user_id" \
  '{title:$title,priority:$priority,assigneeUserId:$assignee,labels:["backend","tenant-safe"]}')
http_json POST "/api/v1/projects/$project_id/work-items" \
  "$contributor_token" "$workspace_id" "$work_item_payload"
expect_status 201 "work-item creation"
work_item_id=$(jq -er '.id' <<< "$HTTP_BODY")
created_version=$(jq -er '.version' <<< "$HTTP_BODY")

update_payload=$(jq -cn --arg title "Deliver secure tenant workflow" --arg priority Critical \
  --arg assignee "$contributor_user_id" --arg version "$created_version" \
  '{title:$title,priority:$priority,assigneeUserId:$assignee,labels:["api","security"],expectedVersion:$version}')
http_json PATCH "/api/v1/work-items/$work_item_id" \
  "$contributor_token" "$workspace_id" "$update_payload"
expect_status 200 "work-item update"
updated_version=$(jq -er '.version' <<< "$HTTP_BODY")

transition_payload=$(jq -cn --arg status InProgress --arg version "$updated_version" \
  '{targetStatus:$status,expectedVersion:$version}')
http_json POST "/api/v1/work-items/$work_item_id/transitions" \
  "$contributor_token" "$workspace_id" "$transition_payload"
expect_status 200 "work-item transition"
current_version=$(jq -er '.version' <<< "$HTTP_BODY")
pass "work item moved from Backlog to InProgress"

step "Checking stale-write and tenant boundaries"
stale_payload=$(jq -cn --arg status Blocked --arg version "$updated_version" \
  '{targetStatus:$status,expectedVersion:$version}')
http_json POST "/api/v1/work-items/$work_item_id/transitions" \
  "$contributor_token" "$workspace_id" "$stale_payload"
expect_status 409 "stale transition"
expect_problem concurrency_conflict
pass "stale version returned 409 Conflict"

http_json GET "/api/v1/work-items/$work_item_id" "$outsider_token" "$outsider_workspace_id"
expect_status 404 "cross-workspace read"
pass "cross-workspace read returned a non-disclosing 404"

step "Waiting for audit and notification evidence"
notification_count=0
for _ in {1..30}; do
  http_json GET '/api/v1/notifications?page=1&pageSize=20' "$contributor_token" "$workspace_id"
  expect_status 200 "notification list"
  notification_count=$(jq -r '.totalCount' <<< "$HTTP_BODY")
  [[ "$notification_count" -gt 0 ]] && break
  sleep 1
done
[[ "$notification_count" -gt 0 ]] || fail "Notification was not delivered within 30 seconds"

http_json GET '/api/v1/audit-events?page=1&pageSize=20&action=work_item.transitioned&entityType=work_item' \
  "$owner_token" "$workspace_id"
expect_status 200 "audit list"
audit_count=$(jq -r '.totalCount' <<< "$HTTP_BODY")
[[ "$audit_count" -gt 0 ]] || fail "Transition audit evidence was not found"
pass "transactional audit and outbox notification are visible"

mkdir -p "$(dirname "$state_file")"
state_temp="$demo_temp_dir/state.json"
jq -n \
  --arg workspaceId "$workspace_id" \
  --arg outsiderWorkspaceId "$outsider_workspace_id" \
  --arg projectId "$project_id" \
  --arg workItemId "$work_item_id" \
  --arg staleVersion "$updated_version" \
  --arg currentVersion "$current_version" \
  '{workspaceId:$workspaceId,outsiderWorkspaceId:$outsiderWorkspaceId,projectId:$projectId,workItemId:$workItemId,staleVersion:$staleVersion,currentVersion:$currentVersion}' \
  > "$state_temp"
mv "$state_temp" "$state_file"

show_summary "$workspace_id" "$project_id" "$work_item_id"
