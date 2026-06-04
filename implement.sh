#!/usr/bin/env bash
set -Eeuo pipefail

MODEL="gpt-5.4-mini"

usage() {
  cat >&2 <<'EOF'
Usage: ./implement.sh <prompt-or-prompt-file> <target-directory>

Examples:
  ./implement.sh VibeCoding.md vibe-coding
  ./implement.sh "write pragmatic code" pragmatic
EOF
}

die() {
  echo "error: $*" >&2
  exit 1
}

require_command() {
  command -v "$1" >/dev/null 2>&1 || die "required command not found: $1"
}

if [[ $# -ne 2 ]]; then
  usage
  exit 2
fi

prompt_arg=$1
target_dir=$2

case "$target_dir" in
  ""|/*|.*|*/../*|../*|*/..)
    die "target directory must be a relative path inside the repository and must not contain '..' or start with '.'"
    ;;
esac

require_command codex
require_command git
require_command rsync
require_command mktemp
require_command python3

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

[[ -d specs ]] || die "specs/ directory not found"
[[ -f specs/FEATURE_01.md ]] || die "specs/FEATURE_01.md not found"
[[ -f specs/FEATURE_10.md ]] || die "specs/FEATURE_10.md not found"

if [[ -f "$prompt_arg" ]]; then
  style_prompt=$(<"$prompt_arg")
else
  style_prompt=$prompt_arg
fi

[[ -n "${style_prompt//[[:space:]]/}" ]] || die "prompt must not be empty"

if [[ -e "$target_dir" && ! -d "$target_dir" ]]; then
  die "target path exists but is not a directory: $target_dir"
fi

if [[ -e "$target_dir" ]]; then
  target_status=$(git status --porcelain -- "$target_dir")
  if [[ -n "$target_status" ]]; then
    die "target directory has uncommitted changes; commit or remove them first: $target_dir"
  fi
fi

mkdir -p "$target_dir"

run_id=$(date -u +"%Y%m%dT%H%M%SZ")
run_dir=".runs/${target_dir}/${run_id}"
mkdir -p "$run_dir"

prompt_source="literal"
if [[ -f "$prompt_arg" ]]; then
  prompt_source=$prompt_arg
fi

python3 - "$run_dir/run.json" "$run_id" "$target_dir" "$MODEL" "$prompt_source" "$(date -u +"%Y-%m-%dT%H:%M:%SZ")" <<'PY'
import json
import sys
from pathlib import Path

Path(sys.argv[1]).write_text(json.dumps({
    "run_id": sys.argv[2],
    "target_dir": sys.argv[3],
    "model": sys.argv[4],
    "prompt_source": sys.argv[5],
    "started_at_utc": sys.argv[6],
}, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY

printf '%s\n' "$style_prompt" >"$run_dir/style-prompt.txt"

tmp_root=""
cleanup() {
  if [[ -n "$tmp_root" && -d "$tmp_root" ]]; then
    rm -rf "$tmp_root"
  fi
}
trap cleanup EXIT

sync_project() {
  local source_dir=$1
  local destination_dir=$2

  rsync -a --delete \
    --exclude '.git' \
    --exclude 'bin/' \
    --exclude 'obj/' \
    --exclude '.vs/' \
    --exclude 'TestResults/' \
    "$source_dir"/ "$destination_dir"/
}

write_feature_metrics() {
  local events_log=$1
  local metrics_path=$2
  local feature_number=$3
  local spec_path=$4
  local started_at=$5
  local finished_at=$6
  local duration_seconds=$7
  local codex_exit_code=$8
  local commit_sha=$9
  local numstat_path=${10}

  python3 - "$events_log" "$metrics_path" "$feature_number" "$spec_path" "$started_at" "$finished_at" "$duration_seconds" "$codex_exit_code" "$commit_sha" "$numstat_path" <<'PY'
import json
import sys
from collections import Counter
from pathlib import Path

events_path = Path(sys.argv[1])
metrics_path = Path(sys.argv[2])
feature_number = sys.argv[3]
spec_path = sys.argv[4]
started_at = sys.argv[5]
finished_at = sys.argv[6]
duration_seconds = int(sys.argv[7])
codex_exit_code = int(sys.argv[8])
commit_sha = sys.argv[9]
numstat_path = Path(sys.argv[10])

event_count = 0
event_types = Counter()
function_calls = 0
function_call_names = Counter()
command_calls = 0
apply_patch_calls = 0
token_sums = Counter()
token_maxima = Counter()
last_message = None
session_id = None

TOKEN_KEYS = {
    "input_tokens",
    "output_tokens",
    "cached_input_tokens",
    "reasoning_output_tokens",
    "total_tokens",
    "prompt_tokens",
    "completion_tokens",
}

def is_token_key(key):
    return key in TOKEN_KEYS or key.endswith("_tokens")

def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)

def first_string(*values):
    for value in values:
        if isinstance(value, str) and value:
            return value
    return None

with events_path.open("r", encoding="utf-8", errors="replace") as events:
    for line in events:
        line = line.strip()
        if not line:
            continue
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue

        event_count += 1
        event_type = first_string(event.get("type"), event.get("event"), event.get("name")) or "unknown"
        event_types[event_type] += 1

        session_id = session_id or first_string(event.get("session_id"), event.get("sessionId"))

        for obj in walk(event):
            obj_type = first_string(obj.get("type"), obj.get("event"), obj.get("name"))
            if obj_type:
                lowered = obj_type.lower()
                if "function_call" in lowered or lowered in {"tool_call", "tool-call"}:
                    function_calls += 1
                    name = first_string(obj.get("name"), obj.get("tool_name"), obj.get("recipient_name"))
                    if name:
                        function_call_names[name] += 1
                if lowered in {"exec", "exec_command", "shell_command"} or "exec_command" in lowered:
                    command_calls += 1
                if "apply_patch" in lowered:
                    apply_patch_calls += 1

            name = first_string(obj.get("name"), obj.get("tool_name"), obj.get("recipient_name"))
            if name:
                if "exec" in name:
                    command_calls += 1
                if "apply_patch" in name:
                    apply_patch_calls += 1

            for key, value in obj.items():
                if is_token_key(key) and isinstance(value, int):
                    token_sums[key] += value
                    token_maxima[key] = max(token_maxima[key], value)

            message = first_string(
                obj.get("message"),
                obj.get("text"),
                obj.get("content"),
                obj.get("last_message"),
                obj.get("final_message"),
            )
            if message:
                last_message = message

files_changed = insertions = deletions = 0
if numstat_path.exists():
    with numstat_path.open("r", encoding="utf-8", errors="replace") as numstat:
        for line in numstat:
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 3:
                continue
            files_changed += 1
            if parts[0] != "-":
                insertions += int(parts[0])
            if parts[1] != "-":
                deletions += int(parts[1])

metrics = {
    "feature": feature_number,
    "spec_path": spec_path,
    "started_at_utc": started_at,
    "finished_at_utc": finished_at,
    "duration_seconds": duration_seconds,
    "codex_exit_code": codex_exit_code,
    "commit_sha": commit_sha,
    "event_count": event_count,
    "event_types": dict(sorted(event_types.items())),
    "function_calls_observed": function_calls,
    "function_call_names": dict(sorted(function_call_names.items())),
    "shell_command_calls_observed": command_calls,
    "apply_patch_calls_observed": apply_patch_calls,
    "token_maxima_observed": dict(sorted(token_maxima.items())),
    "token_sums_observed": dict(sorted(token_sums.items())),
    "git_files_changed": files_changed,
    "git_insertions": insertions,
    "git_deletions": deletions,
    "git_churn": insertions + deletions,
    "session_id": session_id,
    "last_message_excerpt": last_message[-1000:] if last_message else None,
}

metrics_path.write_text(json.dumps(metrics, indent=2, sort_keys=True) + "\n", encoding="utf-8")
print(json.dumps(metrics, sort_keys=True))
PY
}

for feature_number in {01..10}; do
  spec_path="specs/FEATURE_${feature_number}.md"
  [[ -f "$spec_path" ]] || die "missing spec: $spec_path"

  echo "==> Implementing $spec_path into $target_dir with $MODEL"
  feature_log_dir="$run_dir/feature-${feature_number}"
  mkdir -p "$feature_log_dir"

  cleanup
  tmp_root=$(mktemp -d "${TMPDIR:-/tmp}/codex-implement-${feature_number}.XXXXXX")
  tmp_project="$tmp_root/project"
  mkdir -p "$tmp_project"

  sync_project "$target_dir" "$tmp_project"

  spec_content=$(<"$spec_path")

  codex_prompt=$(cat <<EOF
You are implementing one feature in an isolated C#/.NET project workspace.

Hard constraints:
- Use C# and .NET for the implementation.
- Work only in the current working directory.
- Do not read or inspect parent directories, sibling directories, repository root files, or any path outside this workspace.
- Do not use git. The wrapper script will commit after you finish.
- Do not implement future specs unless doing so is strictly necessary to keep the current feature coherent.
- Preserve and build on the existing code in this workspace.
- Prefer the standard .NET SDK and avoid third-party NuGet packages unless the project already uses them.
- Add or update focused tests where practical.
- Run relevant dotnet build/test checks before finishing.

Style/how prompt:
${style_prompt}

Current feature spec:
${spec_content}

Implement this feature completely. Leave the workspace containing the finished project files.
EOF
)
  printf '%s\n' "$codex_prompt" >"$feature_log_dir/prompt.txt"

  events_log="$feature_log_dir/codex-events.jsonl"
  last_message_path="$feature_log_dir/last-message.txt"
  started_epoch=$(date +%s)
  started_at=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

  set +e
  codex \
    --model "$MODEL" \
    --ask-for-approval never \
    exec \
    --cd "$tmp_project" \
    --sandbox workspace-write \
    --skip-git-repo-check \
    --ephemeral \
    --json \
    --output-last-message "$last_message_path" \
    - <<<"$codex_prompt" >"$events_log"
  codex_status=$?
  set -e

  finished_epoch=$(date +%s)
  finished_at=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  duration_seconds=$((finished_epoch - started_epoch))

  if [[ "$codex_status" -ne 0 ]]; then
    write_feature_metrics "$events_log" "$feature_log_dir/metrics.json" "$feature_number" "$spec_path" "$started_at" "$finished_at" "$duration_seconds" "$codex_status" "" "$feature_log_dir/numstat.tsv" >>"$run_dir/summary.jsonl"
    die "codex failed while implementing $spec_path"
  fi

  sync_project "$tmp_project" "$target_dir"

  git add -- "$target_dir"

  if git diff --cached --quiet -- "$target_dir"; then
    die "codex produced no staged changes for $spec_path"
  fi

  git commit -m "Implement feature ${feature_number} in ${target_dir}" -- "$target_dir"
  commit_sha=$(git rev-parse HEAD)
  git show --numstat --format='' "$commit_sha" -- "$target_dir" >"$feature_log_dir/numstat.tsv"
  write_feature_metrics "$events_log" "$feature_log_dir/metrics.json" "$feature_number" "$spec_path" "$started_at" "$finished_at" "$duration_seconds" "$codex_status" "$commit_sha" "$feature_log_dir/numstat.tsv" >>"$run_dir/summary.jsonl"
done

python3 - "$run_dir/summary.jsonl" "$run_dir/summary.json" <<'PY'
import json
import sys
from pathlib import Path

summary_jsonl = Path(sys.argv[1])
summary_json = Path(sys.argv[2])
rows = []
if summary_jsonl.exists():
    with summary_jsonl.open("r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if line:
                rows.append(json.loads(line))

totals = {
    "features": len(rows),
    "duration_seconds": sum(row.get("duration_seconds", 0) for row in rows),
    "event_count": sum(row.get("event_count", 0) for row in rows),
    "function_calls_observed": sum(row.get("function_calls_observed", 0) for row in rows),
    "shell_command_calls_observed": sum(row.get("shell_command_calls_observed", 0) for row in rows),
    "apply_patch_calls_observed": sum(row.get("apply_patch_calls_observed", 0) for row in rows),
    "git_files_changed": sum(row.get("git_files_changed", 0) for row in rows),
    "git_insertions": sum(row.get("git_insertions", 0) for row in rows),
    "git_deletions": sum(row.get("git_deletions", 0) for row in rows),
    "git_churn": sum(row.get("git_churn", 0) for row in rows),
    "token_maxima_observed": {},
    "token_sums_observed": {},
}

for row in rows:
    for key, value in row.get("token_sums_observed", {}).items():
        totals["token_sums_observed"][key] = totals["token_sums_observed"].get(key, 0) + value
    for key, value in row.get("token_maxima_observed", {}).items():
        totals["token_maxima_observed"][key] = max(totals["token_maxima_observed"].get(key, 0), value)

summary_json.write_text(json.dumps({"totals": totals, "features": rows}, indent=2, sort_keys=True) + "\n", encoding="utf-8")
PY

echo "Done. Implemented features 01-10 in $target_dir."
echo "Run logs: $run_dir"
