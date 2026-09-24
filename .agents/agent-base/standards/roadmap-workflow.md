# Roadmap Workflow

Canonical reference for how goals and roadmap rules apply day to day: checking alignment
before work starts, and the exact status vocabulary.

**Audience**: human contributors and coding agents.

## Goals alignment (AB-GOALS-001)

Before each change, name the Objective it serves and check whether the approach contradicts a
current Strategy choice. When the change plausibly moves that Objective, load its file and name
the Key Result and direction of movement. If no Objective applies, ask whether to proceed rather
than silently continuing.

When the human approves work outside a Goal or against another Venture entity, record one
`**Venture exception**: <entity> -- <why>` field on its roadmap item. Use the most specific entity
that makes the deviation findable, such as `Goal 003` or `Strategy`; do not create a separate drift
log. One exception is an accepted trade-off. Repeated exceptions are evidence for
`venture-review`, not an automatic block.

If the project has no objectives defined yet, say so before substantial work starts -- don't
let work proceed against an undefined target. Catalog: `.agents/goals/_GOALS_CATALOG.md`.

## Before starting a task (AB-ROADMAP-001)

Before starting any task:

1. Read `_ROADMAP_CATALOG.md` and any relevant roadmap files.
2. List `.agents/sessions/` -- the filenames alone are enough, since the leading timestamp already
   sorts them -- and read the few most recent entries that look relevant to this task or to an
   `In Progress` item, plus at least the single most recent one regardless of topic. This is a
   cross-check, not a survey: never read the whole directory as routine practice. Recent agents
   record what they were actually mid-flight on there, and a session naming unfinished work whose
   roadmap item is not `In Progress`, or whose stated status disagrees with the item's, is a
   discrepancy to reconcile and mention before proceeding, not a detail to skip past.
3. State one sentence, before planning or implementing, on how the task serves the roadmap.
4. If it looks off-roadmap or low priority, warn the user and list 1-2 higher-priority
   alternatives from `In Progress`/`Open` rows.
5. Wait for explicit confirmation before proceeding on a warned task -- don't treat silence or
   a topic change as approval.

### Work discovered inside other work

The trigger is leaving unfinished work, not only working off-roadmap. A detour is often a
perfectly good roadmap item, which is exactly why it slips through: nothing warns, and the
original task is quietly abandoned. Treat "I should fix this first" as the trigger regardless of
whether the new thing is on the roadmap.

Name the detour, say what it interrupted, and offer three outcomes rather than one gate:

| Outcome | Use when |
| --- | --- |
| Do it now, no roadmap item | It is genuinely small: minutes, one obvious correction, no design choice, and describing it would cost more than doing it |
| Roadmap it and return | It is real work, and the current task is worth finishing first |
| Roadmap it and do it now | It is real work and it blocks the current task, or the context to do it well exists only right now |

The middle and last options both produce a roadmap item, so the decision is only about attention,
never about whether the work gets recorded. Reserve the first option honestly: "small" means small
to do, not small to describe, and a detour that turns out larger than claimed becomes an item at
the moment that becomes clear.

Record the suspended work in the handoff's detour stack per `AB-SESSION-001`, then announce the
return when the detour ends. A detour whose real home is agent-base is a request; see
`AB-REQUEST-001`.

### Corrections and lessons (AB-ROADMAP-005)

A meaningful correction from the human, or an outcome the agent observes that could generalize
beyond the current task, is proposed as a roadmap `Idea` (or `Open`, when its scope is already
clear) before the turn ends. Unlike an ordinary discovered-work detour, this step has no "too
small to record" exception: the cost of proposing one line is far below the cost of a lesson
disappearing, which is exactly what Goal 004 forbids. Recording and implementing stay two separate
approvals -- the human may accept the record and decline or defer the implementation in the same
breath, and a merely recorded item carries no authorization to build it.

A correction is durable knowledge by definition: it is the thing the next session needs and would
otherwise repeat. So it goes in the roadmap, which is what a later session reads before acting, and
not in tool memory, which is for what dies with this working stretch. `DOC-003` draws that line by
lifetime rather than by storage, and holding a lesson anywhere that ends with the session is the
same as not having learned it.

This is the lightweight reflex, not the full mechanism. Item 059 builds deduplication, evidence
reuse, and measured improvement on top of it; this rule only guarantees that nothing is silently
dropped in the meantime.

## Status vocabulary (AB-ROADMAP-004)

A roadmap item's `Status` is exactly one of: `Idea`, `Open`, `In Progress`, `Done`, `Blocked`,
`Deferred`, `Archived` -- never `Closed` or an invented synonym. `agent-base-guard.sh` fails
the session on any other value.

Deciding between the two closing states: `Done` means the work succeeded as scoped; `Archived`
means it didn't happen as scoped (superseded, abandoned, no longer relevant) -- not a
judgment on whether trying was worthwhile.

## Proactive completion (AB-ROADMAP-002)

When the definition of done is met, close the work in the same turn instead of asking for a
separate approval round:

1. Verify the definition of done and relevant checks.
2. Set every completed in-scope item to `Done`; use `Archived` only when it did not succeed as
   scoped. Move each row out of `_ROADMAP_CATALOG.md` into the matching section of
   `_ROADMAP_HISTORY.md` at the same time, unchanged apart from its completion note.
3. Report the evidence and any hesitation, compromise, omitted scope, or follow-up concern.
4. Invite the human to object if the completion claim is wrong. Otherwise hand off the review,
   commit, and push using `AB-GIT-002`.

The uncommitted diff is the human acceptance gate. If the human disagrees, immediately correct
the work or restore the appropriate non-terminal status before commit. Do not leave work open
merely to obtain a second message containing closure approval.

## Superseded work (AB-ROADMAP-002, AB-ROADMAP-003)

Checking history before adding an item answers "have we already done this?". It never answers the
inverse: "does this make something we already did obsolete?". A closed item is not read again, so
work built for conditions that no longer hold keeps its `Done` status while its rules, hooks, and
collectors keep running. The cost surfaces much later as behavior nobody can justify and
measurement of a problem that already went away.

Run the same search at both ends of an item's life and act on it differently:

| Moment | Question | Outcome |
| --- | --- | --- |
| Opening | Which earlier items pursued this same goal? | Inherit their findings, or state plainly that this supersedes them |
| Closing | Which earlier items assumed conditions this one just changed? | Re-evaluate each: still valid, partly obsolete, or `Archived` as superseded |

The closing sweep is required only when the item delivered a **mechanism**: a new capability that
changes how a goal is reached, not a fix inside an existing one. Adopting vendor sandboxing,
introducing a generator, and moving a manual check into a guard are mechanisms; correcting a
rule's wording is not.

Search by goal, not by title or number. A predecessor rarely shares vocabulary with its
replacement, because the replacement is named after the technology while the predecessor was named
after the symptom. Re-evaluating a predecessor is itself real work: record what the sweep found and
offer it per "Work discovered inside other work" instead of silently expanding the current task.

## Two indexes, one row (AB-ROADMAP-002, AB-ROADMAP-003)

`AB-ROADMAP-001` makes `_ROADMAP_CATALOG.md` mandatory reading before every task, so whatever
stays in it is paid for on every task forever. Finished work is evidence rather than a pending
decision, so it is kept but not carried:

- `_ROADMAP_CATALOG.md` holds `In Progress`, `Open`, and `Idea`. It is always-on context.
- `_ROADMAP_HISTORY.md` holds `Done` and `Archived`. It is loaded only on demand.

A row lives in exactly one of them, decided by its own `Status`, and never in both. The numbered
item file never moves and keeps its permanent number, so no history is lost or renumbered by the
split; only the row's location changes.

Two consequences follow, and neither is optional:

- Closing an item is a move between files, not a move between sections.
- `AB-ROADMAP-003` deduplication must read `_ROADMAP_HISTORY.md` before proposing a new item.
  Half of what it has to check, everything already finished or abandoned, is no longer in the
  catalog. Skipping it silently reintroduces work someone already rejected.

Repositories created before this contract keep every row in the catalog until a human approves
the migration; nothing moves their content automatically.

## Full titles before shorthand (AB-REF-001)

When a numeric roadmap or migration reference has no full title stated anywhere in the current or
immediately preceding response, resolve it first: look up `_ROADMAP_CATALOG.md` or the item file
and state its full title before answering, deciding, or acting from it.

Within one response, later mentions of the same item may shorten progressively:

- A second mention may pair the number with a short nickname instead of the full title (e.g.
  `090 - AGENTS.md v2`), not the number alone.
- A third or later mention may use the bare number alone.

This survives context compaction better than assuming "it was already clear earlier": a title that
only appeared many turns back is not assumed still available, but one from the immediately
preceding response is.

## Cross-references

- Behavior rules and rule IDs: see `AGENTS.md`
- Idea/step/archived dedup (`AB-ROADMAP-003`): see
  `.agents/roadmap/_ROADMAP_CATALOG.md` and `.agents/roadmap/_ROADMAP_HISTORY.md`
