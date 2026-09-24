#!/usr/bin/env bash
set -euo pipefail

# Thin project wrapper for the agent-base worktree consolidation engine.
#
# It declares nothing itself. The trunk name and the two-letter repo_id both come from the single
# table in .agents/standards/development-workflow.md; the engine discovers the <REPO_ID><N> slot
# worktrees and their _<repo_id><N> branches from that code. Fill that table in once, at adoption.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
WORKFLOW="$ROOT_DIR/.agents/standards/development-workflow.md"
remote='origin'

if [[ ! -f "$WORKFLOW" ]]; then
  printf 'ERROR %s is missing; cannot read the development workflow.\n' "$WORKFLOW" >&2
  exit 2
fi

# Pull one backtick-wrapped value cell out of a table row: | `field` | `value` | ... |
read_field() {
  { grep -E "^\|[[:space:]]*\`$1\`[[:space:]]*\|" "$WORKFLOW" \
    | head -1 \
    | sed -E "s/^\|[[:space:]]*\`$1\`[[:space:]]*\|[[:space:]]*\`?([^\`|]*)\`?[[:space:]]*\|.*/\1/" \
    | tr -d '[:space:]'; } 2>/dev/null || true
}

stable_branch="$(read_field stable_branch)"
repo_id="$(read_field repo_id)"
[[ -n "$stable_branch" && "$stable_branch" != 'TODO' ]] || stable_branch='main'

if [[ ! "$repo_id" =~ ^[a-z]{2}$ ]]; then
  printf 'ERROR repo_id in %s must be two lowercase letters (got: %s).\n' \
    "$WORKFLOW" "${repo_id:-empty}" >&2
  exit 2
fi

exec bash "$ROOT_DIR/scripts/worktree-consolidate.sh" \
  --integration-branch "$stable_branch" \
  --remote "$remote" \
  --repo-id "$repo_id" \
  "$@"
