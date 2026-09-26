# Roadmap

**Last updated**: 2026-09-26

This file holds current and planned work only. Finished and abandoned items keep their permanent
rows in [`_ROADMAP_HISTORY.md`](_ROADMAP_HISTORY.md), which is not loaded at ordinary task start.

> **For agents (see rule `AB-ROADMAP-001` in `AGENTS.md`):** This file is the source of truth for
> the roadmap. Before starting any task, read this file and every other `*.md` file in
> `.agents/roadmap/` relevant to the current milestone. State one sentence on how the task
> contributes to the roadmap before planning or implementing. If the task looks off-roadmap or
> low priority, warn the user and offer 1–2 higher-priority alternatives from the step tables
> below.

---

## Release goal

<!-- TODO: Define your release targets. Example:

| Target | Release date |
| --- | --- |
| Platform A | Q2 2026 |
| Platform B | Q3 2026 |
-->

## Current status

<!-- TODO: Summarize current readiness. Example:

- **Release phase**: pre-release development
- **Strengths**: ...
- **Primary gaps**: ...
-->

## Performance requirements

<!-- TODO: List any hard performance requirements that are release blockers. -->

## Exit criteria

<!-- TODO: Define what "done" means for each milestone. Example:

### Milestone 1

- No open critical blockers
- CI release pipeline proven on release candidates
- Store review approvals accepted
-->

## Feature tracking

Canonical lists: each Product's `.agents/products/<product>/features/_FEATURE_CATALOG.md`.

<!-- TODO: List release-blocking features. Example:

| Feature | Status | Notes |
| --- | --- | --- |
| `NNN-feature-name.md` | Partial | Must be complete before launch |
-->

Features not listed here are out of release scope unless explicitly promoted.

---

## Status definitions

One file and one row here for the item's entire life, from the moment it is first written
down. `Status` is what tracks where in the lifecycle it currently is - exactly one of these
seven values, nothing else (`AB-ROADMAP-004`; `agent-base-guard.sh` fails the session on any
other value):

| Status | Meaning |
| --- | --- |
| Idea | Not yet committed to. Free to promote (-> `Open`) or archive at any time. |
| Open | Not yet started. Ready to be picked up. |
| In Progress | Actively being worked. |
| Done | Definition of done met. It succeeded. |
| Blocked | Cannot proceed; waiting on an external dependency or decision. |
| Deferred | Explicitly pushed out of current milestone scope. Stays where it is. |
| Archived | Abandoned or failed as scoped: an idea that was dropped or a step that did not work out, no difference in mechanics. The numbered file never moves; its row moves to the `Archived` section of `_ROADMAP_HISTORY.md`. |

There is no `Closed`. Finishing something means deciding which of `Done` or `Archived`
actually applies, and writing that word - never an invented synonym.

---

## In Progress

| # | Item | Milestone | Notes |
| --- | --- | --- | --- |
| 001 | [Home screen 1:1 visual parity](001-home-screen-visual-parity.md) | Vertical slice | Building/screenshotting on connected Galaxy Z Fold |
| 010 | [Results screen v1](010-results-screen-v1.md) | Vertical slice | Foundation built, critical ScrollView flex-shrink layout bug found+fixed; post-session context re-verification still open |

---

## Open

| # | Item | Milestone | Notes |
| --- | --- | --- | --- |
| 002 | [Shell navigation functional end-to-end](002-shell-navigation-functional.md) | Vertical slice | Depends on: - |
| 003 | [All three worlds playable end-to-end](003-worlds-playable-end-to-end.md) | Vertical slice | Depends on: - |
| 004 | [Secondary screens visual polish](004-secondary-screens-visual-polish.md) | Vertical slice | Depends on: -; lower priority than 001-003 |
| 005 | [Show status bar, hide only nav bar](005-immersive-os-chrome.md) | Vertical slice | Depends on: -; nav-hide done+verified, status-bar-visible unresolved, iOS unverified |
| 006 | [Integrate Inter font](006-integrate-inter-font.md) | Vertical slice | Depends on: -; needs sourcing font files |

---

## Idea

| # | Item | Milestone | Notes |
| --- | --- | --- | --- |
| 008 | [Carousel live drag tracking](008-carousel-live-drag-tracking.md) | - | Swipe today jumps only on release; investigated root cause + options, not yet actioned |
| 009 | [Real audiometric-difficulty scoring](009-real-audiometric-scoring.md) | - | Points-per-catch is a frequency-only placeholder; needs real per-trial volume/audibility data |

---

## Done and Archived

Terminal items live in [`_ROADMAP_HISTORY.md`](_ROADMAP_HISTORY.md), not here, so this file stays
cheap enough to read before every task. Their rows are kept permanently; only their location
changes.

---

## Rules

- **One file, one row, for the item's entire life.** Filename: `NNN-kebab-name.md`, from
  [_ROADMAP_TEMPLATE.md](_ROADMAP_TEMPLATE.md). No separate ideas file and no `archive/`
  subdirectory. An idea, a step, and an archived item are the same kind of thing at different
  points in one lifecycle; `Status` tracks the point. The row lives in exactly one of the two
  indexes, never both: this catalog while the item is live, `_ROADMAP_HISTORY.md` once it is
  `Done` or `Archived`.
- **Status is exactly one of seven values** - see "Status definitions" above. There is no
  `Closed` and no other invented word.
- Numbers reflect discovery order, not execution order or priority - an idea and a step draw
  from the same sequence, so a number is unambiguous without knowing which kind of item it is.
- A step doc defines **work to be done**, never Feature behavior (link to its Product-scoped
  Feature document for that).
- **Refactorings are roadmap steps.** There is no separate `refactorings/` folder. If
  architectural refactoring work is required, file it here as a roadmap step whose name
  contains the word `refactoring` (e.g. `007-refactoring-data-layer.md`). Capture the problem,
  options considered, chosen option, rationale, and key findings in the step doc's `Notes`.
- **An idea is a real entry, kept deliberately light.** `Status: Idea`, one or two sentences
  under "What needs to happen", `Milestone` and `Depends on` left as `-`, no `Definition of
  done`. It should be readable in fifteen seconds and writable in one minute. Promoting it is
  a one-word edit: `Idea` -> `Open`, then fill in the rest of the template.
- **Check for a matching entry before adding anything new.** Read
  [`_ROADMAP_HISTORY.md`](_ROADMAP_HISTORY.md) first and scan its `Done` and `Archived` rows
  together with the `Idea` section above, looking for a related item already tried, rejected, or
  abandoned, and its recorded reason, before proposing the same thing again (`AB-ROADMAP-003`).
- **Dropping or abandoning something is also a one-word edit.** Set `Status: Archived` in the
  file, write why in `Notes`, and move its row to the `Archived` section of `_ROADMAP_HISTORY.md`.
- On completion set `Status: Done` in the file and move its row to the `Done` section of
  `_ROADMAP_HISTORY.md` in the same turn. Follow `AB-ROADMAP-002` in `AGENTS.md` for proactive
  closure and human review through the git handoff.
