#!/usr/bin/env bash
# check-safe-001-reminder.sh - cheap, safe UserPromptSubmit refresh of AB-SAFE-001.
#
# Roadmap item 123, Tier 1 (the universal half): fires on every message for both Claude Code
# and Codex (vendor-hooks.md), which directly counters the measured decay of a requirement-type
# rule over a long session (rule-writing.md's cited Gamage 2026 finding) -- the reminder never
# has to survive more than one turn's recall, because it is reissued fresh every turn it is
# relevant. Two hard requirements, same as check-pending-migrations.sh:
#   1. Must be fast - one stdin read, no network, no expensive git calls.
#   2. Must always exit 0 - UserPromptSubmit can block (and erase) the prompt on exit 2, so a
#      bug here must never turn into a silently eaten user message.
#
# Prints one short line only when the submitted prompt text plausibly involves a credential.
# Prints nothing and exits 0 otherwise, or if anything looks unexpected (unreadable stdin,
# missing python3) - silence is always the safe default here.

set -uo pipefail

payload="$(cat)"

prompt="$(python3 -c '
import json, sys
try:
    data = json.loads(sys.argv[1])
except Exception:
    print("")
    raise SystemExit
print(data.get("prompt", ""))
' "$payload" 2>/dev/null)" || exit 0

[[ -z "$prompt" ]] && exit 0

pattern='password|secret|api[_ -]?key|token|credential|\.env\b|private[_ -]?key|ssh[_ -]?key|access[_ -]?key'
if printf '%s' "$prompt" | grep -qiE "$pattern"; then
  echo "Reminder (AB-SAFE-001): before handling any credential here, load this repository's own .agents/agent-base/standards/secrets.md first, unconditionally -- and if the file involved belongs to a different repository than this session's own root, that repository's own copy, not only the one already loaded here."
fi

exit 0
