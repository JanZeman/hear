#!/usr/bin/env bash
# Safe MCP connection diagnostic. See .agents/agent-base/standards/mcp-credential-diagnostics.md.
set -euo pipefail
script_dir="$(cd -- "$(dirname -- "$0")" && pwd)"
exec python3 "$script_dir/diagnose-mcp-credential-failure.py" "$@"
