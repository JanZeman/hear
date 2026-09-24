# Git

Canonical reference for this project's git-related rules: what language commits must be in,
what agents may and may not run, and how commit suggestions work.

**Audience**: human contributors and coding agents.

## Committed-content language (AB-DOC-005)

Conversational language may switch freely, following the user. Anything committed to git --
commit messages, code, comments, docstrings, file/branch names -- must always be English,
regardless of chat language. Translate non-English content before writing it; never commit it
verbatim.

## What agents may run (AB-GIT-001)

Human controls Git mutations. Agents may run read-only Git and stash operations. They must not
stage, commit, push, merge, rebase, reset, checkout, switch, tag, or perform another Git mutation
except for the narrow submodule, worktree, and verified Agent Base update provisions below.

Agents must also not discard uncommitted work: `git restore`, `git checkout <path>`, and
`git clean` need the human's explicit request. These leave history untouched, so the
history-changing category above does not reach them, yet they are the most destructive commands an
agent can run: the work they delete was never committed, so no history exists to recover it from.
Reverting an agent's own unwanted edit is not an exception -- propose it and let the human decide.

**Submodule pointer updates are also allowed**, narrowly: git commands run *inside* a
submodule's own directory to move it to a different commit (`git -C <submodule-path> checkout
<sha>`, `git -C <submodule-path> pull`, `git submodule update [--remote]`). This exception is only
for advancing a submodule's own checkout, never the project's own working tree. It does not permit
staging the resulting pointer.

**Convention-defined worktree-slot provisioning is also allowed**, narrowly: only after a human
explicitly requests it and the agent has loaded
`.agents/agent-base/skills/create-worktrees-by-convention/SKILL.md`, the agent may run exactly the
declared N verified `git worktree add -b <slot-branch> <sibling-path> <base-ref>` commands from the
project's `.agents/standards/development-workflow.md`, one per `<REPO_ID><N>` slot in that
standard's declared count. This exception creates the complete declared set, not an arbitrary
worktree or a partial set. The skill owns its read-only preflight and postflight checks. It never authorizes a
branch switch, integration, commit, push, worktree removal, Git permission change, or automatic
repair after partial creation.

**Verified Agent Base updates are also allowed**, narrowly: a direct human request to `Update AB`,
`publish AB`, or update one named client or the fleet authorizes the loaded update procedure to
stage, commit, and push the verified Agent Base release and audited Agent Base sync paths in that
named scope. It requires the procedure's clean-baseline, release, path-review, required-check, and
fresh-remote gates to pass. A failed gate, unexplained path, unresolved drift, or migration needing
judgment ends the exception for that repository; do not commit or push it. This exception never
authorizes merge, rebase, reset, force push, branch switching, product changes, or unrelated
cleanup.

**One-way worktree-slot propagation is also allowed**, narrowly: immediately after the above
exception commits a verified Agent Base sync on a repository's integration branch, the agent may
run that repository's worktree-consolidation wrapper with `--to-slots-only` (for example
`worktrees.sh --to-slots-only`) to merge the updated integration branch into each already-clean,
declared worktree slot. This authorizes only the `<integration> -> slot` direction; it never
authorizes merging a slot's own commits into the integration branch, which needs a human's
judgment about whether that work is ready. A slot that fails the engine's own preflight (dirty,
mid-operation, unsyncable submodule) is left alone and reported; it does not fall back to the
full bidirectional `worktrees.sh`, a repair attempt, or any other Git mutation. Pushing the
propagated slot branches (`--push`) is allowed under this same exception; the integration branch's
own push, if needed, remains the update procedure's separate concern above.

Creating, editing, moving, and deleting files is always allowed -- this rule is about git
commands specifically, not file changes.

If a task genuinely requires a Git mutation outside these provisions, explain why you can't run it
and tell the user the exact command to run themselves instead. State the boundary and hand off
flatly; do not also offer to run it "if given permission" or "if you'd like me to" -- that framing
invites exactly the back-and-forth the handoff exists to avoid.

**Enforcement**: Claude Code additionally blocks history mutations mechanically via
`.claude/settings.json` deny lists. Other vendors rely on this contract and their sandbox or
reviewer boundaries.

## Commit suggestions (AB-GIT-002)

At every completed-work handoff, resolve the current worktree root with
`git rev-parse --show-toplevel`, then show these commands for the human and never run their Git
operations, unless a verified direct `Update AB`, `publish AB`, or named fleet update owns the
handoff. Replace the first line with the verified absolute path, retaining the quotes:

```
~~~~~~~
cd "<absolute current-worktree root>"
git add .
git commit -m "<principle-centered English message>"
~~~~~~~
```

The path prevents later commands from running in whichever terminal directory happens to be
active. The human reviews what `git add .` will include before running it. Add `git push` after the
commit command only when remote publication is required. Fence the block on its own with a plain
`~~~~~~~` line directly above and below the commands, outside any code fence, so the block visually
stands apart from surrounding prose; the human copies exactly what sits between the two tilde lines.

Immediately after the command block, add exactly one localized next-step section. In Czech, its
heading is `Návrh dalšího kroku:`. Prefer the next unresolved step in the human-approved plan or
conversation queue; otherwise choose the highest-priority relevant roadmap work. If neither
exists, say that no next step remains instead of inventing scope.

The commit subject is short, one line, and names the most important behavioral or product
principle of the change. The message is that subject alone; a body appears only when the human
asks for one. **The message always starts with a capital letter.** A subject may open
with a lowercase identifier prefix -- a project's own lowercase name, a command, a filename --
and that prefix keeps the spelling the identifier actually has; the capital applies to the
message that follows it, never to the prefix. So `agent-base v0.0.X: Publish verified fleet
candidates`, not `... publish verified fleet candidates`. A version number, filenames, "update",
"sync", or other surface-only description is insufficient by itself. Do not use a Conventional
Commits prefix unless the project requires it. For an agent-base release, use
`agent-base v0.0.X: <Main behavioral principle>`. For a downstream sync, retain the version and
the release principle, e.g. `Sync with agent-base v0.0.X: <Main behavioral principle>`.

Do not include `git push` for an ordinary local checkpoint. Include it for an Agent Base release,
an explicit publication request, or a workflow that requires a pushed result. A verified direct
Agent Base update is the narrow exception that may execute those commands itself.

**Running a shown block on the human's reply (`AB-GIT-002`).** A bare `k` after this block keeps
`AB-INPUT-001`'s ordinary meaning and does not authorize running it. `kk` or `ok` -- not bare `k`
-- means the human wants the shown block run on their behalf: the commit still carries the
human's own configured Git identity, the agent only executes it. Run
`scripts/run-confirmed-commit.sh --root "<the exact cd path just shown>" --message "<the exact
message just shown>"` from that same root (agent-base's own upstream root instead runs
`template/scripts/run-confirmed-commit.sh`, since it has no synced top-level copy of its own
scripts), adding `--push` only if the block itself included `git push`. Never edit the root or the
message before running them, and never run this for a block a different reply is answering -- it
authorizes exactly the block just displayed, once, and lapses the moment another response is
shown. This is a narrow, explicit exception to `AB-GIT-001`, not a general grant to run Git
commands after any short reply.

**Syncing agent-base** remains routine maintenance, not a project roadmap step -- don't file
one merely because files synchronized.

The user may ignore any of this at any time.

## Cross-references

- Behavior rules and rule IDs: see `AGENTS.md`
- Overriding any of these defaults for this project: see `documentation.md`
