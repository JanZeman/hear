#!/usr/bin/env bash
# ab-update.sh - user-facing entry point for this downstream repository.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REAL="$ROOT/_sub/ai-venture/ab-update-apply.sh"

if [[ ! -f "$REAL" ]]; then
  echo "✘ $REAL not found - is the ai-venture submodule initialized? Try:" >&2
  echo "    git submodule update --init _sub/ai-venture" >&2
  exit 1
fi

exec bash "$REAL" "$@"
