# [Item Name - verb phrase, e.g. "Submit app to store"]

**Status**: Open
**Milestone**: <!-- TODO: Set milestone, e.g. Q2-2026 -->
**Depends on**: -
<!-- **Venture exception**: <entity> -- <why> -->

<!-- Venture exception (AB-GOALS-001): add this only when the human approves work outside a Goal
     or against another Venture entity. Omit it otherwise -- most items never need it. Name the
     most specific entity, such as Goal 003 or Strategy, so one grep finds repeated deviations.
     There is no separate drift log; this field on the item is the whole mechanism. -->

<!-- Status values are exactly these seven, nothing else (AB-ROADMAP-004):
     Idea | Open | In Progress | Done | Blocked | Deferred | Archived
     There is no "Closed". Finishing something means deciding which of Done (it succeeded)
     or Archived (it did not happen as scoped) actually applies -- never invent a new word. -->

<!-- Idea: not yet committed to. This is the same file and the same catalog row an idea will
     have for its entire life -- there is no separate ideas file. Keep it light: Status: Idea,
     Milestone and Depends on left as "-", one or two sentences under "What needs to happen",
     and Definition of done omitted entirely. It should be readable in fifteen seconds and
     writable in one minute. Promoting it later is a one-word edit: Status: Idea -> Open, then
     fill in the rest of this template. -->

<!-- Archived: will not be finished or adopted as scoped -- an abandoned step or a dropped
     idea, no difference in mechanics. Set Status to Archived and explain why in Notes. The
     file does not move and the catalog row does not move; it just belongs in the catalog's
     Archived section from then on. Deferred means "not now, still planned"; Archived means
     "not happening as scoped." -->

<!-- Refactoring work: include the word "refactoring" in the filename
     (e.g. NNN-refactoring-data-layer.md) and expand Notes to capture the
     problem, options considered, chosen option, rationale, and key findings. -->

<!-- AGENT-BASE-MIGRATION: <marker> -->
<!-- Auto-created items only (apply-agent-base.sh's ensure_migration_roadmap_item): marks an
     item as a machine-registered pending upgrade migration rather than ordinary hand-written
     work. Do not add this marker by hand to a normal item -- it is what
     check-pending-migrations.sh greps for to proactively surface pending migrations to the
     agent (see Tool Adapter Policy in AGENTS.md). -->

## What needs to happen

<!-- TODO: Concrete description of the work. What exactly must be done? Keep it action-oriented.
     For an Idea, one or two sentences is enough -- do not over-invest before it is committed. -->

## Definition of done

<!-- Omit this section entirely for Status: Idea. -->

- [ ] Criterion 1
- [ ] Criterion 2

## Notes

<!-- Running log: discoveries, blockers, decisions, completion date. Newest entry first. -->
