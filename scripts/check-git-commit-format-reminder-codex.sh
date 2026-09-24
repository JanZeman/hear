#!/usr/bin/env bash
# check-git-commit-format-reminder-codex.sh - Codex UserPromptSubmit wrapper around
# check-git-commit-format-reminder.sh.
#
# Codex's UserPromptSubmit hook requires structured JSON output
# (hookSpecificOutput.additionalContext) rather than the plain stdout Claude Code accepts for
# the same event; this script only adds that wrapping around the shared, vendor-neutral check.
# Same safety rule as the wrapped script: always exit 0.
#
# NOTE: the exact Codex hook config format (.codex/hooks.json, this JSON shape) is based on
# published documentation at the time this was written, not verified against a live Codex
# session the way the Claude Code side was.
set -uo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
check="$repo_root/_sub/agent-base/template/scripts/check-git-commit-format-reminder.sh"
[[ -f "$check" ]] || exit 0

msg="$(bash "$check" 2>/dev/null)" || exit 0
[[ -z "$msg" ]] && exit 0

python3 -c '
import json, sys
print(json.dumps({"hookSpecificOutput": {"hookEventName": "UserPromptSubmit", "additionalContext": sys.argv[1]}}))
' "$msg" 2>/dev/null || true
