#!/usr/bin/env bash
# check-staffing-recovery-codex.sh - Codex UserPromptSubmit wrapper around
# check-staffing-recovery.sh.
#
# Codex's UserPromptSubmit hook requires structured JSON output
# (hookSpecificOutput.additionalContext) rather than the plain stdout Claude Code accepts for the
# same event; this script only adds that wrapping around the shared, vendor-neutral check. Same
# safety rule as the wrapped script: always exit 0, never risk blocking or erasing the prompt.
#
# NOTE: as with the other Codex hook wrappers here, the exact .codex/hooks.json schema follows
# published documentation rather than a verified live session. If it never fires, check Codex's
# current hooks documentation for schema changes before assuming the detection logic is wrong.
set -uo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
check=""
for candidate in \
  "$repo_root/_sub/agent-base/template/scripts/check-staffing-recovery.sh" \
  "$repo_root/template/scripts/check-staffing-recovery.sh"; do
  [[ -f "$candidate" ]] && { check="$candidate"; break; }
done
[[ -z "$check" ]] && exit 0

msg="$(bash "$check" 2>/dev/null)" || exit 0
[[ -z "$msg" ]] && exit 0

python3 -c '
import json, sys
print(json.dumps({"hookSpecificOutput": {"hookEventName": "UserPromptSubmit", "additionalContext": sys.argv[1]}}))
' "$msg" 2>/dev/null || true
