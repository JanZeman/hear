#!/usr/bin/env bash
# check-pending-migrations.sh - cheap, safe check for pending agent-base migrations.
#
# Designed to run as a UserPromptSubmit hook (Claude Code, Codex), which fires on every
# single message. Two hard requirements that shape everything here:
#   1. Must be fast - no git calls, no network, just a grep over a handful of small files.
#   2. Must always exit 0 - UserPromptSubmit can block (and erase) the prompt on exit 2,
#      so a bug here must never turn into a silently eaten user message.
#
# Prints one short line if any roadmap item is Status: Open/In Progress and carries either
# the unique migration marker or durable generated-origin metadata.
# Prints nothing and exits 0 if there is nothing pending, or if anything looks unexpected
# (missing directory, unreadable files) - silence is always the safe default here.

set -uo pipefail  # deliberately not -e: this script must reach its own exit 0 no matter what

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
roadmap_dir="$repo_root/.agents/roadmap"
[[ -d "$roadmap_dir" ]] || exit 0

count=0
for f in "$roadmap_dir"/*.md; do
  [[ -f "$f" ]] || continue
  # Skip catalog/template/etc. -- their leading underscore excludes them from numbered
  # roadmap items, and _ROADMAP_TEMPLATE.md documents the metadata syntax itself.
  [[ "$(basename "$f")" == _* ]] && continue
  if { grep -q "AGENT-BASE-MIGRATION:" "$f" 2>/dev/null ||
       grep -Fq "<!-- Auto-created by apply-agent-base.sh. Introduced in:" "$f" 2>/dev/null; } &&
     grep -qE '^\*\*Status\*\*: (Open|In Progress)' "$f" 2>/dev/null; then
    count=$((count + 1))
  fi
done

if [[ "$count" -gt 0 ]]; then
  echo "Note: $count agent-base migration(s) pending (Status: Open/In Progress, .agents/roadmap/*.md, durable agent-base migration metadata). Mention this to the user before addressing their message, if not already discussed this session, and offer to handle it -- don't act on it silently."
fi

exit 0
