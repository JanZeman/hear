#!/usr/bin/env bash
# vendor.sh - audit and safely converge machine-wide Agent Base vendor settings.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -f "$ROOT_DIR/scripts/agent-autonomy.py" ]]; then
  AUTONOMY_SCRIPT="$ROOT_DIR/scripts/agent-autonomy.py"
elif [[ -f "$ROOT_DIR/template/scripts/agent-autonomy.py" ]]; then
  AUTONOMY_SCRIPT="$ROOT_DIR/template/scripts/agent-autonomy.py"
else
  echo "agent-autonomy.py not found beside vendor.sh" >&2
  exit 1
fi

usage() {
  cat <<'USAGE'
Usage:
  vendor.sh --codex|--claude|--copilot|--warp|--all
  vendor.sh --support-status
  vendor.sh --codex|--claude|--copilot --restore BACKUP_DIR

The default action audits the selected vendor first. If managed settings need to
change, the command shows the proposal and accepts only an explicit `yes` before
creating timestamped backups and applying Agent Base-owned values.
USAGE
}

vendor=""
restore_dir=""
support_status=false
while [[ $# -gt 0 ]]; do
  case "$1" in
    --codex|--claude|--copilot|--warp|--all)
      if [[ -n "$vendor" ]]; then
        echo "Select exactly one vendor option." >&2
        usage >&2
        exit 2
      fi
      vendor="${1#--}"
      shift
      ;;
    --support-status)
      if [[ -n "$vendor" || "$support_status" == true || -n "$restore_dir" ]]; then
        echo "--support-status cannot be combined with another option." >&2
        usage >&2
        exit 2
      fi
      support_status=true
      shift
      ;;
    --restore)
      if [[ "$support_status" == true ]]; then
        echo "--restore cannot be combined with --support-status." >&2
        usage >&2
        exit 2
      fi
      if [[ $# -lt 2 ]]; then
        echo "--restore requires a backup directory." >&2
        exit 2
      fi
      restore_dir="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ "$support_status" == true ]]; then
  if [[ -f "$ROOT_DIR/scripts/vendor-support.py" ]]; then
    exec python3 "$ROOT_DIR/scripts/vendor-support.py" status
  fi
  exec python3 "$ROOT_DIR/template/scripts/vendor-support.py" status
fi

if [[ -z "$vendor" ]]; then
  echo "Select one of --codex, --claude, --copilot, --warp, or --all." >&2
  usage >&2
  exit 2
fi

confirm() {
  local prompt="$1" answer=""
  printf '%s Type yes to continue: ' "$prompt"
  if ! IFS= read -r answer; then
    echo >&2
    echo "No consent received; no settings were changed." >&2
    return 1
  fi
  if [[ "$answer" != "yes" ]]; then
    echo "Consent declined; no settings were changed."
    return 1
  fi
}

if [[ -n "$restore_dir" ]]; then
  if [[ "$vendor" == "all" || "$vendor" == "warp" ]]; then
    echo "Restore requires one file-backed vendor: --codex, --claude, or --copilot." >&2
    exit 2
  fi
  echo "Restore proposal: replace the current $vendor settings with backup $restore_dir."
  echo "The current settings file will receive its own timestamped backup first."
  confirm "Restore $vendor settings?" || exit 0
  exec python3 "$AUTONOMY_SCRIPT" restore \
    --vendor "$vendor" --backup "$restore_dir" --consent-recorded
fi

echo "Agent Base vendor settings audit: $vendor"
set +e
python3 "$AUTONOMY_SCRIPT" status --vendor "$vendor" --repo "$ROOT_DIR"
status=$?
set -e
if [[ $status -eq 0 ]]; then
  echo "No managed machine settings changes are required."
  exit 0
fi
if [[ $status -ne 1 ]]; then
  echo "Audit failed with status $status; no settings were changed." >&2
  exit "$status"
fi

echo
echo "Proposal: apply only the Agent Base-owned values listed above."
echo "Every existing settings file that changes will be backed up first."
confirm "Apply this proposal for $vendor?" || exit 0
python3 "$AUTONOMY_SCRIPT" install \
  --vendor "$vendor" --repo "$ROOT_DIR" --consent-recorded
