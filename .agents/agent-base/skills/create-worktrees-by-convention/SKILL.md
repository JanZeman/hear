---
name: create-worktrees-by-convention
description: Create the worktree slots declared by a project's development-workflow standard, after a strict preflight and only when the human explicitly requests it.
---

# Create worktrees by convention

Use this skill only after a human explicitly asks to create the project's convention-defined
worktree slots. It is the sole `AB-GIT-001` exception for this operation. Never use it to create a
partial set, an arbitrary worktree, a disposable investigation worktree, or to repair an earlier
failure.

## Read the declaration

In a downstream repository, read these files first:

1. `.agents/agent-base/standards/git.md`
2. `.agents/standards/development-workflow.md` (for `repo_id` and the trunk name)

In the standalone agent-base source, use
`template/.agents/agent-base/standards/git.md` and `.agents/standards/development-workflow.md`
instead.

The project's One-time setup block must contain N direct commands, one per declared slot, each in
this exact shape:

```bash
git worktree add -b <slot-branch> <sibling-path> <base-ref>
```

where `<slot-branch>` is `_<repo_id><N>`, `<sibling-path>` is `../<REPO_ID><N>`, and `<base-ref>`
is the trunk (`stable_branch`, default `main`). `N` runs `1..count` with no gaps.

Treat the commands as immutable data, not a template to improvise. Stop without mutation and
report the missing or ambiguous declaration if any command differs from that shape, if the set is
empty, if the numbering has a gap, if `repo_id` is not two lowercase letters, if the main worktree
folder is not the uppercase `repo_id`, or if the standard does not explicitly permit this skill.

Before the generic shape check, compare the block against the table's `repo_id` and
`stable_branch`. The literal `_xx1`/`../XX1` scaffold means adoption is incomplete: report that
specific cause and direct the human to rerun Agent Base after filling `repo_id`, or to replace the
project-owned declaration deliberately. For every other mismatch, say whether the branch name,
sibling path, base reference, or slot numbering differs from the declaration. Do not derive a
replacement during provisioning and do not run any Git command until the declaration is coherent.

## Preflight

From the main repository worktree, verify all of the following before running any command:

- `git rev-parse --show-toplevel` is the current directory and `git rev-parse --git-dir` equals
  `git rev-parse --git-common-dir`, proving this is the main rather than a linked worktree.
- The main worktree folder name equals the uppercase `repo_id` (e.g. `AB` for `ab`).
- `git status --porcelain=v1 --untracked-files=all` is empty.
- Every base reference resolves, and each equals the branch currently checked out in the primary
  worktree.
- None of the slot branches already exist.
- None of the target paths exist, in `git worktree list --porcelain` or on the filesystem.
- Each target is an immediate sibling of the primary repository, not a nested or unrelated path.
- The commands create distinct branches and distinct target paths, and contain no shell control
  operators, redirections, substitutions, or extra arguments.

If any check fails or cannot be established, stop without mutation. Say which fact prevented safe
provisioning and leave correction to the human.

## Create and verify

Run the declared commands in order, unchanged and separately. Do not add `&&`, a wrapper, a
retry, or a cleanup command.

If one command succeeds and a later one fails, stop. Report every successfully created slot and
the failing command exactly; do not remove any created worktree or branch and do not retry.

When every command succeeds, initialize submodules in each new worktree:

```bash
git -C <sibling-path> submodule update --init --recursive
```

Then show `git worktree list --porcelain` and `git -C <sibling-path> status --short` for every new
slot. Do not switch branches, integrate work, commit, push, alter Git permissions, or perform any
further Git mutation.
