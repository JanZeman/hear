# Requests to Agent Base

Canonical register of this project's change requests to agent-base, the upstream that supplies
`AGENTS.md`, the guard, the standards, and the skills this repository receives through
`_sub/agent-base`.

This is not the project's roadmap and not Venture feedback. A roadmap item is work this project
will do. Venture feedback is what people say about this project's own products. A request is
something this repository wants **agent-base** to change, because the gap is in the shared
contract rather than in this project.

## Why the file exists

Before this register, an observation about agent-base only existed in flight: an agent noticed it,
said it in chat, and it was gone. Nothing proved later whether it was accepted, refused, or simply
never seen. A request keeps its own permanent file and moves through states, so its outcome is
always visible here.

## How a request is written

- One file per request, `NNN-AB-kebab-name.md`, from `_REQUEST_TEMPLATE.md`. Numbers are permanent
  and record discovery order, exactly like Goals and Roadmap items.
- Fill in `Blocks` when the request interrupted something here, naming a roadmap item or saying in
  one sentence what was suspended and where it stood. It is the return path, and by the time the
  answer arrives the context that made it obvious will be gone.
- Write it from this project's point of view, with real evidence. Naming this repository, its
  paths, and its specifics is expected and correct: they are the evidence. Agent-base is
  responsible for genericizing anything it adopts, and its own files never name this repository.
- One observation per file. Two symptoms with one root cause are one request; the same symptom
  seen twice strengthens the existing request rather than creating a second.
- Never write a request into `_sub/agent-base`. That is a pinned detached checkout and changes
  there do not survive.

## States

This project sets `New` and, at the end, `Resumed`. Agent-base writes everything in between, inside
the request file's managed decision block.

| State | Meaning | Written by |
| --- | --- | --- |
| `New` | Written here, not yet seen by agent-base | this project |
| `Collected` | Agent-base has read it; triage is in progress | agent-base |
| `Accepted` | Agent-base will make the change; the decision block carries its roadmap item ID | agent-base |
| `Refused` | Agent-base will not make the change; the decision block carries the reason | agent-base |
| `Downstream-only` | Valid, but it is this project's change to make, not a generic one | agent-base |
| `Released` | Accepted and shipped; the decision block carries the agent-base version | agent-base |
| `Resumed` | This project has gone back to the work in `Blocks` | this project |

`Resumed` is the only state that is about attention rather than judgment, and it exists because a
request usually interrupts something. Work here reveals a gap in agent-base, the request is
written, the original work stops, and without a closing state nothing ever says that it started
again. The guard keeps naming a `Released` or `Downstream-only` request until it is `Resumed`, so
the thread back to the interrupted work cannot quietly go cold. A `Refused` request is also
answered and gets the same treatment: the answer is no, and the original work still has to be
picked back up or abandoned deliberately.

A handled request is never deleted and its text is never rewritten. Deleting it would destroy the
record and make "refused, with a reason" indistinguishable from "never collected", which is the
failure this register exists to remove.

## Requests

| # | Request | Opened | State | Agent Base item |
| --- | --- | --- | --- | --- |

<!-- TODO: rows are added here as requests are written. Keep this table and each request file's
     header in agreement; the file is the source of truth for its evidence and decision. -->

## Rules

- Before adding a request, read the rows above for a match. Strengthen an existing request with
  new evidence rather than opening a second one.
- A `Refused` request may be reopened only with new evidence, recorded in the same file.
- Agent-base collects these during a fleet update and when this repository syncs, and the guard
  surfaces open ones at session start. Nothing here requires anyone to remember to ask.
