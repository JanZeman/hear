#!/usr/bin/env bash
# check-staffing-recovery.sh - cheap, safe UserPromptSubmit reminder of unfinished staffing work.
#
# Roadmap item 148. This is the second activation channel for Venture Staffing, and it is
# deliberately narrower than "remind me to staff things". A UserPromptSubmit hook cannot observe a
# work-unit boundary: one prompt can grow into several units with no further submission, and a
# condition broad enough to catch that would fire on ordinary prompts until the reader learned to
# skip it. The authoritative check therefore lives at work-unit registration in the staffing
# procedure, and this hook only surfaces the two states that genuinely survive between sessions and
# that nothing else will bring back up:
#
#   1. the worktree's write lease is `uncertain`, so nobody may write until it is cleared; and
#   2. a run holds invocations whose effects nobody observed.
#
# Both are recovery states. Silence is correct and expected the rest of the time.
#
# Two hard requirements, the same as every other UserPromptSubmit check here:
#   1. Must be fast - a couple of small local file reads, no network, no model.
#   2. Must always exit 0 - this event can block and erase the prompt on a non-zero exit, so a bug
#      here must never turn into a silently eaten user message.
#
# It stays silent inside a dispatched invocation (AB_STAFFING_CONTEXT set): a child is not the
# coordinator, and reminding it to recover would invite exactly the recursive staffing that the
# invocation contract exists to prevent.

set -uo pipefail

[[ -n "${AB_STAFFING_CONTEXT:-}" ]] && exit 0

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0

# Root agent-base keeps these under template/; a downstream client receives them under its
# submodule. Neither path existing is the ordinary case for a repository that never staffed
# anything, and it is silent.
lease_script=""
for candidate in \
  "$repo_root/_sub/agent-base/template/scripts/staffing_lease.py" \
  "$repo_root/template/scripts/staffing_lease.py" \
  "$repo_root/scripts/staffing_lease.py"; do
  [[ -f "$candidate" ]] && { lease_script="$candidate"; break; }
done
[[ -z "$lease_script" ]] && exit 0

report="$(python3 "$lease_script" status --root "$repo_root" 2>/dev/null)" || exit 0

case "$report" in
  *uncertain*)
    echo "Reminder (AB-MODEL-001): this worktree's staffing write lease is 'uncertain', so a previous writer may still be running and no route may write here until that is resolved. Load .agents/agent-base/skills/venture-staffing/SKILL.md and follow its recovery steps: stop the identified writer, observe that it stopped, and record that evidence. A confirmation without evidence overrides the guarantee instead of meeting it."
    ;;
  *owned*)
    echo "Reminder (AB-MODEL-001): this worktree's staffing write lease is held by an invocation from an earlier run. If that invocation is genuinely finished its lease was never released, which means its outcome was never recorded either. Load .agents/agent-base/skills/venture-staffing/SKILL.md before writing here."
    ;;
esac

exit 0
