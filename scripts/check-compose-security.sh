#!/usr/bin/env bash

set -euo pipefail

compose_json=$(docker compose config --format json)

for service in postgres rabbitmq redis; do
  if jq -e --arg service "$service" '(.services[$service].ports // []) | length > 0' \
    <<< "$compose_json" >/dev/null; then
    echo "$service must not publish a host port." >&2
    exit 1
  fi
done

for service in api identity; do
  if ! jq -e --arg service "$service" '
    (.services[$service].ports // []) as $ports |
    ($ports | length) > 0 and all($ports[]; .host_ip == "127.0.0.1")
  ' <<< "$compose_json" >/dev/null; then
    echo "$service must publish only loopback host ports." >&2
    exit 1
  fi
done

echo "Compose host-port boundary: PASS"
