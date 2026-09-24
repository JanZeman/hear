#!/usr/bin/env bash
# check-credential-repo-boundary.sh - PreToolUse detector for roadmap item 123, Tier 2.
#
# Vendor-neutral core: reads the hook's raw stdin JSON directly. Claude Code's and Codex's
# PreToolUse payloads share the same tool_name/tool_input/cwd field names (vendor-hooks.md), so
# this one script works unmodified under either, provided its caller forwards stdin unchanged.
# Prints one short line on stdout when either check below fires, prints nothing otherwise, and
# always exits 0 -- this hook only ever adds a reminder, never blocks, so a bug here must never
# turn into a stuck tool call.
#
# Best-effort only: Bash (both vendors) and Codex's apply_patch report their target inside a
# free-form tool_input.command string, not a clean field, so extraction there is a heuristic
# regex, not a parser. Claude's Read/Edit give a clean tool_input.file_path and need no
# heuristic.

set -uo pipefail

payload="$(cat)"

target="$(python3 -c '
import json, re, sys
try:
    data = json.loads(sys.argv[1])
except Exception:
    print("")
    raise SystemExit

tool_input = data.get("tool_input", {}) or {}
path = tool_input.get("file_path", "") or ""
if not path:
    command = tool_input.get("command", "") or ""
    match = re.search(r"(?:^|[\s\x27\"])((?:\.{0,2}/)?[\w./-]+\.[\w-]+)", command)
    path = match.group(1) if match else ""
print(path)
' "$payload" 2>/dev/null)" || exit 0

[[ -z "$target" ]] && exit 0

cwd="$(python3 -c '
import json, sys
try:
    data = json.loads(sys.argv[1])
except Exception:
    print("")
    raise SystemExit
print(data.get("cwd", ""))
' "$payload" 2>/dev/null)" || exit 0

credential_pattern='\.env($|\.[^/]*$)|settings\.local\.json$|credentials\.json$|id_rsa$|\.pem$|\.netrc$'
is_credential=0
printf '%s' "$target" | grep -qE "$credential_pattern" && is_credential=1

is_cross_repo=0
if [[ -n "$cwd" ]]; then
  target_dir="$(dirname "$target")"
  target_root="$(git -C "$target_dir" rev-parse --show-toplevel 2>/dev/null || true)"
  session_root="$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null || true)"
  if [[ -n "$target_root" && -n "$session_root" && "$target_root" != "$session_root" ]]; then
    is_cross_repo=1
  fi
fi

if [[ "$is_credential" -eq 1 || "$is_cross_repo" -eq 1 ]]; then
  reason=""
  [[ "$is_credential" -eq 1 ]] && reason="looks credential-shaped"
  if [[ "$is_cross_repo" -eq 1 ]]; then
    [[ -n "$reason" ]] && reason="$reason and "
    reason="${reason}belongs to a different repository than this session's own root"
  fi
  echo "Reminder (AB-SAFE-001): the target of this action $reason. Before proceeding, load that repository's own .agents/agent-base/standards/secrets.md, unconditionally -- its own copy, not only the one already loaded for this session's root."
fi

exit 0
