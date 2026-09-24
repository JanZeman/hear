# Goals

**Status**: DEFINED

<!-- TODO: Replace the Objectives table below with this project's actual objectives, and
     change Status to DEFINED. Until you do, agent-base-guard.sh warns on every agent
     session that this project has no goals. -->

This file is the catalog of what this project is *for*. It **outranks the roadmap**: a
roadmap step exists to serve a goal, so a roadmap without goals is a list of tasks with no
direction.

Goals deliberately live in their own folder rather than inside `.agents/roadmap/`. The
roadmap is an operational document, edited constantly for operational reasons; anything
stable stored inside it gets edited around and goes quietly out of date.

## How goals are written here

**OKR.** A short qualitative **Objective** saying where the project is going, and
measurable **Key Results** saying how anyone would know it got there.

Three rules apply and none of them is optional:

1. **There is no "achievable".** A goal states what is wanted. Feasibility is examined
   *afterwards* and may revise the goal; it never gets a vote while the goal is being
   written. Let it in and the current implementation defines the ambition, at which point
   the goal degenerates into a description of what already happens.
2. **A key result describes a state of the world, not an activity.** Test: could this be
   true without anyone doing anything? Then it is a result. "Run a weekly review" is an
   activity. "The difference never exceeds X%" is a result.
3. **Every key result names what would falsify it, and the evidence must come from outside
   the project.** A claim nothing can contradict is not knowledge. This is the rule that
   catches a project measuring itself with its own instruments and concluding it is fine.

### The objective line has to pass the friend test

**Say it out loud to someone who does not work on this project -- a friend, a colleague
from another team, someone with no domain knowledge -- and they should understand it and
be able to repeat it back an hour later.**

That is a hard test and it is the point. It rules out three things at once:

- **jargon**, because a layperson stops at the first unfamiliar word;
- **numbers**, which belong in the key results and make an objective unmemorable;
- **compound sentences**, which are usually two objectives wearing one coat.

If the line needs the document to make sense, it is a description rather than a goal.

Each objective gets its own file, `NNN-kebab-name.md`, from `_GOALS_TEMPLATE.md`. This
catalog holds only the objective lines and their status.

### Identifier scheme

- An objective's complete identifier is its three-digit file prefix: `001`, `002`, and so on.
- A key result is qualified by its objective: `001-KR1`, `001-KR2`, and so on. The `KR` number
  is local to that objective and is not zero-padded.
- Do not add an `O` prefix. The location and context already say that these are objectives.
- Objective identifiers are permanent and record the order in which objectives were written;
  they are not an implicit importance ranking.

## Objectives

| # | Objective | Status |
| --- | --- | --- |
| 001 | [Hear demonstrably interests people outside the project](001-hear-demonstrably-interests-outsiders.md) | Draft |

<!-- One objective is deliberately all this Venture has right now: it is a single-person, early
     prototype whose only current question is whether a demo generates real outside interest.
     More objectives can be added once that question is answered. -->

**The numbering is the order objectives were written, not the order of importance.** If
the importance order differs, say so here explicitly.

## Rules

- One file per objective. Filename: `NNN-kebab-name.md`.
- The objective line appears both here and in the objective's own file. The file is the
  source of truth for its key results and its rationale.
- **Changing an objective is a decision, not an edit.** Record what changed and why in that
  objective's Notes, with the date.
- Formulations that were considered and rejected stay recorded in the objective's file, so
  they are not re-litigated months later by someone who was not in the room.
- Every change to the project is judged against these objectives before it is made - see
  `AB-GOALS-001` in `AGENTS.md`. A change that advances one objective at another's expense is
  allowed; an unnamed trade-off is not.

## AB-GOALS-001 mechanics

`AGENTS.md` states the rule; this is how it plays out in practice.

- **This is a continuous check on each change**, not a formality done once at the start of a
  task.
- **If a change serves no objective**, say so and ask whether it should be made at all. This
  never blocks the user's choice - if they want to proceed anyway, they proceed. But before
  starting that work:
  1. State how much of the roadmap is currently unfinished - the same open/done figure
     `agent-base-guard.sh` prints at every session start, read from there rather than
     recomputed differently, so the two never disagree. Informational, not a second gate.
  2. Make sure the task has a roadmap item (creating a minimal one if it doesn't have one
     yet) and add `**Goals drift**: yes -- <why>` to that item's header, next to `Status`.
     There is no separate drift log: the flag lives on the item that already exists, so a
     later pattern is one `grep "Goals drift: yes" .agents/roadmap/*.md` away - a candidate
     for revising or adding an objective, instead of vanishing into separate conversations.
- **If this project has no objectives yet**, or they're still the unfilled template, say so
  and ask the user to define them before substantial work starts. The guard's warning about
  this means nothing can be judged - it is not something to silence.
