---
name: agent-base-sync
description: |
  Force an immediate agent-base sync during an active session, bypassing the guard's 24-hour
  cache, then report synced files, drift, migrations, and any human decision required.
---

# Sync agent-base now

## When to use

- The user asks to update, sync, or refresh agent-base now.
- Recognized shorthand: "Update AB", "ab.sh", or "sync AB". Bare "AB" is ambiguous with A/B
  testing and does not trigger this skill.
- The cached session-start guard reported pending migrations or drift and the user wants a
  fresh check during the same session.

Do not use this for the automatic session-start check. Use it only when a fresh mid-session
sync is requested or needed.

## Why this exists

`agent-base-guard.sh` checks automatically at session start, but caches that check for 24
hours. This procedure calls the sync script directly, bypassing that cache.

Invoking the sync is routine agent-base maintenance, not a downstream product roadmap item.
Do not create one merely because files synchronized. A migration emitted by the sync is
different: it is durable work that follows `AB-ROADMAP-002` and `AB-ROADMAP-004`.

## Steps

### 1. Run the verbose sync

```bash
REPO_ROOT="$(git rev-parse --show-toplevel)"
bash "$REPO_ROOT/ab.sh"
```

`./ab.sh` delegates to `_sub/agent-base/apply-agent-base.sh`. Run the submodule path directly
only when the root shortcut does not exist yet. Do not pass `--quiet`; this procedure needs
the detailed output.

### 2. Read all output before reporting

Check, in order:

1. `=== Summary ===`: version delta and synced or seeded counts.
2. `Drift`: project-owned files requiring review.
3. `Migrations`: distinguish pending work from completed work the agent should close now.
4. The suggested commit line, when present.

If the target version, changed path, migration effect, or required action is ambiguous, do not
guess or make a partial repair. Stop the affected update, name the missing evidence, and ask the
human for it. If the evidence points to an agent-base defect, prepare a copy-paste repair prompt
for an agent-base agent instead.

### 3. Report the result

- If no decision is needed, state briefly what synchronized and the new version, close any
  completed migration items, verify every changed path, and confirm the branch still equals a
  fresh live read of its upstream. The direct `Update AB` request authorizes staging only those
  verified paths, committing `Sync with agent-base vX.Y.Z: Apply verified shared rules`, and
  pushing that commit. Return to the prior task after reporting the commit and push result.
- If a customize-once file has drifted, do the merge now rather than asking first. Compare it
  against `_sub/agent-base/template/<path>`, apply the generic improvement, keep the project's own
  content, and write the review marker the script prints. Leave every one of those edits unstaged,
  the marker included, and leave the mechanical sync output staged.
- One question decides where anything goes: **does a person need to look at it?** Mechanical output
  is staged because nobody does. A customize-once merge is unstaged because somebody customized that
  file, and the marker sits with them because it records that a person looked. Risk is not a second
  axis; it enters through your own confidence. Sure of a merge, prepare it; unsure, leave it
  unstaged too and say what the doubt was. Staging something you did not understand is answering the
  question wrongly.
- Then report once: how many changes are staged, what is waiting unstaged, that the merges are
  additive, and which one or two deserve a closer look and why. A later session must be able to read
  that state from the repository without being told it.
- Stop and ask only for what you genuinely cannot judge: a conflicting change, or a file whose
  customization you do not understand. State that the prior task is paused, not abandoned.
- The commit stays the human's in every case. Staging is preparation and claims nothing about
  review; the commit is where anything enters the project.

## Expected result

- A clear statement of what synchronized or that nothing was due.
- Drift arrives prepared: mechanical changes staged, customize-once merges and the review marker
  unstaged, and one report naming what needs a closer look.
- Every decision that genuinely needed a human surfaced explicitly, and nothing that did not.
- A committed and pushed verified sync, or an explicit explanation of the gate that prevented it.
