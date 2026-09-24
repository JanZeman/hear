#!/usr/bin/env bash
# check-credential-repo-boundary-claude.sh - Claude Code PreToolUse wrapper around
# check-credential-repo-boundary.sh.
#
# Claude Code's PreToolUse only surfaces a reminder to the agent through the structured
# hookSpecificOutput.systemMessage envelope; unlike UserPromptSubmit, plain stdout here falls
# through to the normal permission flow unseen. This script only adds that wrapping around the
# shared, vendor-neutral check. Same safety rule as the wrapped script: always exit 0.
set -uo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
check="$repo_root/_sub/agent-base/template/scripts/check-credential-repo-boundary.sh"
[[ -f "$check" ]] || exit 0

payload="$(cat)"
msg="$(printf '%s' "$payload" | bash "$check" 2>/dev/null)" || exit 0
[[ -z "$msg" ]] && exit 0

python3 -c '
import json, sys
print(json.dumps({
    "hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "allow"},
    "systemMessage": sys.argv[1],
}))
' "$msg" 2>/dev/null || true
