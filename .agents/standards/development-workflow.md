# Development workflow

Canonical reference for how this project branches, releases, and runs parallel work. The
branching model and the worktree layout are one system, so they are defined together here.
Replace every `TODO` once, when adopting agent-base.

**Audience**: human contributors and coding agents.

## Branching model: trunk-based

- One long-lived branch: `main`. It is always releasable.
- No `dev`, `develop`, or separate integration branch. Work merges straight to `main`.
- A release is a tag on `main` (`vX.Y.Z`). There is no permanent release branch. Cut a
  short-lived `release/x.y` only when a specific release genuinely needs stabilisation while
  `main` keeps moving, and delete it after the tag.
- A production fix is a short-lived branch off `main`, merged straight back. There is no
  dedicated hotfix lane.
- Short-lived topic branches are optional: an experiment, or a change you want reviewed as its
  own pull request. When you use one, name it `<prefix><short-description>` with a prefix from
  `topic_prefixes`, and delete it after merge.

Branch deletion is always a manual, human-only action (`AB-GIT-001`).

## Identity and layout

| Field | Value | Meaning |
| --- | --- | --- |
| `repo_id` | `hr` | Exactly two lowercase letters (`^[a-z]{2}$`), unique to this repository. Chosen once at adoption. |
| `stable_branch` | `main` | The trunk. Defaults to `main`; change only if this project's trunk has a different name. |
| `topic_prefixes` | `feature/ bugfix/ ci/` | Space-separated allowed prefixes for the optional short-lived topic branches. |

- The **main worktree** is cloned into a folder named `<REPO_ID>` (the uppercase form of
  `repo_id`, e.g. `AB`). This is enforced: `worktrees.sh` hard-fails and the session guard shows
  a red non-compliance block if the folder name does not match.
- **Linked worktree slots** are sibling folders `<REPO_ID>1`, `<REPO_ID>2`, and so on. Each slot
  has one dedicated branch `_<repo_id><N>` in lowercase (`_ab1` for `AB1`).
- `_<repo_id><N>` carries that slot's work and is merged into `main` on consolidation. When the
  slot is idle it rests on that same branch. It is never renamed to a `feature/*` branch; the
  whole slot is the unit of work.

While `repo_id` is still `TODO` or malformed, the guard warns every session and the worktree
tooling refuses to run rather than guess.

## Why worktrees

A worktree gives you a separate checkout, index, and branch at once. This is ideal for two or
more agents working in parallel, for switching context without `git stash`, and for keeping each
slot's work reviewable as a clean diff against `main`.

## Provisioning the slots

When a human explicitly asks for `create-worktrees-by-convention`, an agent may create the
`git worktree add -b _<repo_id><N> ../<REPO_ID><N> main` commands for the declared slot count,
after loading `.agents/agent-base/skills/create-worktrees-by-convention/SKILL.md`. The skill
verifies the main worktree, every target path, every slot branch, and the base reference before
any mutation, and creates the complete declared set. If one creation succeeds and a later one
fails, it reports the exact state and does not repair it.

After a human fills a valid `repo_id` and reruns Agent Base, the exact untouched scaffold below
becomes the default two slots. It derives `<REPO_ID>` by uppercasing `repo_id`, so `bt` becomes
`BT`, `BT1`, and `BT2`, with branches `_bt1` and `_bt2`. A block that differs from the untouched
scaffold is project-owned: Agent Base leaves it unchanged, and the human declares any different
slot count directly. Keep the commands direct, one per line, and use sibling paths. Any other
creation, branch change, integration, commit, push, or removal remains human-controlled under
`AB-GIT-001`.

One-time setup (default two slots for `repo_id = hr`):

```bash
git worktree add -b _hr1 ../HR1 main
git worktree add -b _hr2 ../HR2 main
```

Work in a slot: open `../HR1`, stay on `_hr1`, and commit there. A human integrates
`_hr1` into `main` when it is ready.

## Consolidation helper

Agent Base seeds a project-owned root `worktrees.sh` wrapper and keeps its generic
`scripts/worktree-consolidate.sh` engine synchronized. The wrapper declares nothing: it reads
`stable_branch` and `repo_id` from the table above and passes `--repo-id` to the engine, which
discovers the `<REPO_ID><N>` slots and merges their `_<repo_id><N>` branches into the trunk and
back. It hard-fails on a main worktree folder that is not `<REPO_ID>`, on a slot that is not on
exactly its own `_<repo_id><N>` branch, and on an orphaned `_<repo_id><N>` branch with no
worktree. It reports and ignores unrelated worktrees.

The wrapper is a human-operated convenience and does not expand agent Git authority, except the
one narrow case in `.agents/agent-base/standards/git.md`'s `AB-GIT-001`: a verified fleet or
`Update AB`/`publish AB` run may pass `--to-slots-only` to propagate its own just-pushed sync
into already-clean slots, one-way (`main` -> slot only, never slot -> `main`). Run the read-only
preflight before the first consolidation or after changing the table:

```bash
./worktrees.sh --check
```

After every slot is ready and clean, consolidate locally, or publish with `--push` (which pushes
only the trunk and participating slot branches):

```bash
./worktrees.sh
./worktrees.sh --push
```

Add `--to-slots-only` to merge `main` into slots without folding any slot's own commits back in --
this is what the narrow fleet-update exception above runs; a human rarely needs it directly. To
hold one slot back, add `--exclude-branch _<repo_id><N>`. Submodule synchronization allows
initializing and fetching from remote by default; add `--no-submodule-fetch` to require
already-local objects instead. Either way, the engine stops rather than detach an attached
submodule branch whose checkout differs from the recorded commit.

## Common pitfalls

- A branch can only be checked out in one worktree at a time.
- Each worktree has its own build artifacts. Machine-wide caches (package managers) are still
  shared.
- Idle slot branches are checked for cleanliness but excluded from merging and pushing.
- A new worktree starts with **empty submodule directories**: `git worktree add` does not
  initialize submodules, so `_sub/agent-base` and any others stay empty until checked out.
  `worktrees.sh` initializes them automatically; outside it, run
  `git submodule update --init --recursive` in the new slot.

## Submodules across worktrees

Every project on agent-base has at least one submodule (`_sub/agent-base`); some have more. Git
treats a submodule as a pointer to a commit, kept in the index separately from the submodule's own
checkout. The two go out of step easily, and worktrees make it more likely.

**Why it bites.** `git merge` updates the pointer, never the checkout. After a merge that moves a
submodule forward, the slot's index says commit B while the submodule's own directory on disk is
still commit A. Symptoms:

- `git status` in the slot lists the submodule as modified although nothing was changed.
- `git submodule status` shows the path prefixed with `+`.
- `ignore = dirty` in `.gitmodules` does **not** hide this -- it suppresses changes *inside* the
  submodule's working tree, not a pointer mismatch.
- Anything reading the submodule reads the old content: a stale `_sub/agent-base` means
  `agent-base-guard.sh` runs an outdated agent-base without saying so.

**The trap.** Committing that modified submodule pointer in the slot records a *rollback*. The
next consolidation merges the rollback into the trunk and silently downgrades the submodule
repo-wide. Never commit a submodule pointer you did not intend to move. Check with:

```bash
git diff --submodule=short -- _sub/
```

**The fix.** Re-check-out the recorded commits:

```bash
git submodule update --init --recursive
```

`worktrees.sh` does this automatically, before its clean check and after every merge, but only
when nothing can be lost. It stops instead if a submodule has uncommitted changes, is in a merge
conflict, or sits on a detached commit no ref contains -- resolve those inside the submodule
yourself; per the `AB-GIT-001` override in `AGENTS.md`, checkout inside a submodule is
human-controlled.

**Pointer conflicts.** When the trunk and a slot both move the same submodule, the merge reports
`CONFLICT (submodule)`. There is no textual merge to do: decide which commit is correct, check it
out inside the submodule, stage the submodule path, and commit.

**Pushing.** `./worktrees.sh --push` pushes with `--recurse-submodules=check`, so a branch
recording a submodule commit that was never pushed is refused rather than published. If that
fires, push the submodule's own branch from inside the submodule first.

## Agent session guidance

At session start an agent identifies which worktree it is in and stays there for the whole
session. Never assume a sibling worktree's files are visible or relevant; two agents on different
worktrees cannot see each other's uncommitted changes.

**Rule:** one agent = one worktree = one branch.

## Useful commands

```bash
git worktree list          # list all worktrees
git worktree prune         # clear metadata for a manually deleted worktree
```

## Cross-references

- Behaviour rules: see `AGENTS.md`
- Git authority and staging: see `.agents/agent-base/standards/git.md`
- Formatting: see `.agents/standards/formatting.md`
