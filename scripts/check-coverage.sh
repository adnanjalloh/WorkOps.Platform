#!/usr/bin/env bash

set -euo pipefail

report_path=${1:-}
line_threshold=${2:-70}
branch_threshold=${3:-35}

if [[ -z "$report_path" || ! -f "$report_path" ]]; then
  echo "Coverage report not found: ${report_path:-<missing>}" >&2
  exit 2
fi

if [[ ! "$line_threshold" =~ ^[0-9]+([.][0-9]+)?$ || ! "$branch_threshold" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
  echo "Coverage thresholds must be numeric percentages." >&2
  exit 2
fi

coverage_tag=$(sed -n '/<coverage /p' "$report_path" | head -n 1)
line_rate=$(sed -n 's/.*line-rate="\([0-9.]*\)".*/\1/p' <<< "$coverage_tag")
branch_rate=$(sed -n 's/.*branch-rate="\([0-9.]*\)".*/\1/p' <<< "$coverage_tag")

if [[ -z "$line_rate" || -z "$branch_rate" ]]; then
  echo "Coverage rates could not be read from $report_path." >&2
  exit 2
fi

awk -v line_rate="$line_rate" -v branch_rate="$branch_rate" \
  -v line_threshold="$line_threshold" -v branch_threshold="$branch_threshold" '
  BEGIN {
    line_percent = line_rate * 100
    branch_percent = branch_rate * 100
    printf "Line coverage: %.1f%% (required %.1f%%)\n", line_percent, line_threshold
    printf "Branch coverage: %.1f%% (required %.1f%%)\n", branch_percent, branch_threshold

    if (line_percent + 0.0001 < line_threshold || branch_percent + 0.0001 < branch_threshold) {
      print "Coverage gate failed." > "/dev/stderr"
      exit 1
    }
  }
'
