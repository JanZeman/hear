#!/usr/bin/env bash
# check-credential-repo-boundary-codex.sh - Codex PreToolUse wrapper around
# check-credential-repo-boundary.sh.
#
# Codex's PreToolUse reminder envelope is hookSpecificOutput.additionalContext, not Claude
# Code's systemMessage; this script only adds that wrapping around the shared, vendor-neutral
# check. Same safety rule as the wrapped script: always exit 0.
#
# NOTE: the exact Codex hook config format (.codex/hooks.json, this JSON shape) is based on
# published documentation at the time this was written, not verified against a live Codex
# session the way the Claude Code side was. If it doesn't fire, check Codex's current hooks
# docs for schema changes before assuming the detection logic itself is wrong.
set -uo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
check="$repo_root/_sub/agent-base/template/scripts/check-credential-repo-boundary.sh"
[[ -f "$check" ]] || exit 0

payload="$(cat)"
msg="$(printf '%s' "$payload" | bash "$check" 2>/dev/null)" || exit 0
[[ -z "$msg" ]] && exit 0

python3 -c '
import json, sys
print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "additionalContext": sys.argv[1]}}))
' "$msg" 2>/dev/null || true
