#!/usr/bin/env bash
# check-git-commit-format-reminder.sh - cheap, safe UserPromptSubmit refresh of AB-GIT-002's
# exact commit-block format.
#
# Same shape and same reason as check-safe-001-reminder.sh: a positive, "always do X" formatting
# requirement (tilde-fenced block, one-line commit subject, propose a next step right after)
# measurably decays over a long session (rule-writing.md's cited Gamage 2026 finding), so it gets
# reissued fresh every turn instead of relying on turns-old recall. Two hard requirements, same
# as every other UserPromptSubmit check in this repo:
#   1. Must be fast - one git call, no network.
#   2. Must always exit 0 - UserPromptSubmit can block (and erase) the prompt on exit 2, so a
#      bug here must never turn into a silently eaten user message.
#
# Prints one short line only when the current repo has uncommitted changes (the moment a commit
# suggestion could plausibly be relevant). Prints nothing and exits 0 otherwise, or if anything
# looks unexpected (not a git repo) - silence is always the safe default here.

set -uo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0
[[ -n "$(git -C "$repo_root" status --porcelain 2>/dev/null)" ]] || exit 0

echo "Reminder (AB-GIT-002): this repo has uncommitted changes. If you hand off a commit suggestion, use exactly the tilde-fenced block (cd, git add ., one-line git commit -m \"...\", optional git push) and propose exactly one next step immediately after it."

exit 0
