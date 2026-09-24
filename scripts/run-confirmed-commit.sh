#!/usr/bin/env bash
set -euo pipefail

# Runs one already-shown AB-GIT-002 commit-suggestion block exactly as displayed, under the
# narrow `kk`/`ok` exception in git.md. This script decides nothing on its own: the caller must
# already hold a human reply of `kk` or `ok` (not bare `k`) to the exact block being run here, and
# must pass the identical root, message, and push flag that block showed. A bare `k` never reaches
# this script -- it keeps AB-INPUT-001's ordinary meaning and AB-GIT-001 still governs.

usage() {
  cat <<'USAGE'
Usage: run-confirmed-commit.sh --root PATH --message MESSAGE [--push]

  --root PATH     Absolute worktree root the shown block's `cd` line named.
  --message TEXT  The exact commit message shown in the block. Not reformatted or edited.
  --push          Only if the shown block itself included `git push`.
USAGE
}

root=''
message=''
do_push=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --root)
      root="${2:-}"
      shift 2
      ;;
    --message)
      message="${2:-}"
      shift 2
      ;;
    --push)
      do_push=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 2
      ;;
  esac
done

if [[ -z "$root" || -z "$message" ]]; then
  echo "ERROR --root and --message are both required." >&2
  usage
  exit 2
fi
if [[ "$root" != /* ]]; then
  echo "ERROR --root must be an absolute path, matching the block's cd line exactly." >&2
  exit 2
fi
if [[ ! -d "$root" ]]; then
  echo "ERROR --root does not exist: $root" >&2
  exit 2
fi

toplevel="$(cd "$root" && git rev-parse --show-toplevel)"
if [[ "$toplevel" != "$root" ]]; then
  echo "ERROR --root ($root) is not itself a worktree top level (found: $toplevel)." >&2
  exit 2
fi

cd "$root"
git add .
git commit -m "$message"
if [[ "$do_push" == true ]]; then
  git push
fi
