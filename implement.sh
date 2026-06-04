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

for feature_number in {01..10}; do
  spec_path="specs/FEATURE_${feature_number}.md"
  [[ -f "$spec_path" ]] || die "missing spec: $spec_path"

  echo "==> Implementing $spec_path into $target_dir with $MODEL"

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

  if ! codex exec \
    --model "$MODEL" \
    --cd "$tmp_project" \
    --sandbox workspace-write \
    --ask-for-approval never \
    --skip-git-repo-check \
    --ephemeral \
    - <<<"$codex_prompt"; then
    die "codex failed while implementing $spec_path"
  fi

  sync_project "$tmp_project" "$target_dir"

  git add -- "$target_dir"

  if git diff --cached --quiet -- "$target_dir"; then
    die "codex produced no staged changes for $spec_path"
  fi

  git commit -m "Implement feature ${feature_number} in ${target_dir}" -- "$target_dir"
done

echo "Done. Implemented features 01-10 in $target_dir."
