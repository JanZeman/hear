#!/usr/bin/env bash
set -euo pipefail

# Runs one already-shown AB-GIT-002 commit-suggestion block exactly as displayed, under the
# narrow `kk` exception in git.md. The caller must already hold a human reply starting with `kk` to
# the exact block being run here, pass that reply verbatim as --reply, and pass the identical root, message,
# and push flag that block showed. Only `kk` authorizes: `ok`, a bare `k`, and every other short
# reply keep their ordinary meaning (AB-INPUT-001), so this script refuses them and AB-GIT-001
# still governs. The commit carries the repository's configured Git identity and nothing else.

usage() {
  cat <<'USAGE'
Usage: run-confirmed-commit.sh --reply REPLY --root PATH --message MESSAGE [--push]

  --reply TEXT    The human's reply to the shown block, verbatim. It authorizes a run only when
                  its first word is `kk`; any comment after that word is allowed.
  --root PATH     Absolute worktree root the shown block's `cd` line named.
  --message TEXT  The exact commit message shown in the block. Not reformatted or edited.
  --push          Only if the shown block itself included `git push`.
USAGE
}

reply=''
root=''
message=''
do_push=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --reply)
      reply="${2:-}"
      shift 2
      ;;
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

if [[ -z "$reply" || -z "$root" || -z "$message" ]]; then
  echo "ERROR --reply, --root, and --message are all required." >&2
  usage
  exit 2
fi
# The reply authorizes when its first word is `kk`; anything after it ("kk, commit only") is a
# comment that may narrow what the caller does next but never widens this script. The word ends at the first character that is
# not a letter or digit, and case is ignored, as AB-INPUT-001 does for dictated input.
_first_word="$(printf '%s' "$reply" | tr '\n\t' '  ' | tr '[:upper:]' '[:lower:]' | sed -E 's/^[[:space:]]+//; s/[^[:alnum:]].*$//')"
if [[ "$_first_word" != "kk" ]]; then
  echo "ERROR only a reply whose first word is \`kk\` authorizes running a shown commit block; got: $reply" >&2
  echo "      \`ok\`, a bare \`k\`, and other replies keep their ordinary meaning. Nothing was run." >&2
  exit 3
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

# The identity is the repository's own Git config. Environment overrides would replace it
# silently, so drop them; an unset value stops the run instead of falling back to a default.
unset GIT_AUTHOR_NAME GIT_AUTHOR_EMAIL GIT_COMMITTER_NAME GIT_COMMITTER_EMAIL
if [[ -z "$(git config user.name || true)" || -z "$(git config user.email || true)" ]]; then
  echo "ERROR Git identity is not configured for $root; nothing was run." >&2
  echo "      Ask the human to set it in this repository, then show the block again:" >&2
  echo "        git config user.name \"<your name>\"" >&2
  echo "        git config user.email \"<your email>\"" >&2
  exit 4
fi
if printf '%s\n' "$message" | grep -Eiq '^co-authored-by:|^[^[:alnum:]]*generated with '; then
  echo "ERROR the message carries an attribution trailer; AB-GIT-002 forbids it. Nothing was run." >&2
  exit 2
fi

git add .
git commit -m "$message"
if [[ "$do_push" == true ]]; then
  git push
fi
