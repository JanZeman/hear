#!/usr/bin/env bash
# apply-ai-venture.sh - thin root-level delegator, so you can run ./apply-ai-venture.sh
# instead of the full ./_sub/ai-venture/apply-ai-venture.sh path.
#
# All the real logic lives in the submodule; this file only forwards to it, unchanged
# arguments and all (e.g. `./apply-ai-venture.sh --quiet` still works). Do not add logic
# here -- if you're looking at this file because something needs fixing, the real
# implementation is at _sub/ai-venture/apply-ai-venture.sh (upstream, not editable from a
# downstream repo -- see AGENTS.md's Source of Truth Map).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REAL="$ROOT/_sub/ai-venture/apply-ai-venture.sh"

if [[ ! -f "$REAL" ]]; then
  echo "✘ $REAL not found -- is the ai-venture submodule initialized? Try:" >&2
  echo "    git submodule update --init _sub/ai-venture" >&2
  exit 1
fi

exec bash "$REAL" "$@"
