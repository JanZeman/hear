# Venture Staffing

Canonical definition of how a coherent WORKLOAD is staffed with a WORKER, and of the boundaries
that hold while it runs. Extends `model-routing.md`, which owns the single-hand-off case; this file
owns work that spans more than one WORKLOAD or more than one WORKER.

**Audience**: human contributors and coding agents.

## The rule

> Staff each coherent WORKLOAD with the MODEL and reasoning effort whose expected total cost of
> completing it successfully is lowest, counting model cost, reasoning cost, delegation and
> context-transfer overhead, retries, failure risk, and verification cost.

Expected total cost is a way of thinking, not a number this repository computes. Nothing here
multiplies a probability by a price. What is recorded is what can be observed: attempts,
escalations, turns, and vendor-reported token counts, with "unknown" said plainly where a vendor
reports nothing.

## Vocabulary

The Venture is a firm-shaped descriptive model. Technical compatibility fields may still say
`actor` or `vendor`, but they are legacy field names and not the vocabulary agents use in prose.

| Term | Meaning |
| --- | --- |
| **VENTURE** | The independently managed endeavor. |
| **DEPARTMENT** | Enduring responsibility perspective that leads an outcome (`AB-DEPT-001`). |
| **POSITION** | Optional recurring specialization in one DEPARTMENT, such as Flutter Engineer. Multiple WORKERS may hold one POSITION. |
| **WORKER** | A human or AI worker assigned to a DEPARTMENT. An AI WORKER performs work through a tool, the current SESSION, a host-native helper, or a CLI SESSION. |
| **ROLE** | A WORKER's temporary function in one WORKLOAD, such as executor, reviewer, or coordinator. It is not a catalogue or organizational tree. |
| **WORKLOAD** | One coherent block of work, large enough to repay handoff overhead. |
| **CAPABILITY** | AI-provider-neutral requirement: `tool`, `cheap`, `standard`, `strong`, `expert`. |
| **SESSION** | One concrete instance of a RUNTIME with its own context and lifecycle. |
| **RUNTIME** | The client or host that runs a SESSION. |
| **AI PROVIDER** | The organization that supplies a RUNTIME or MODEL. |
| **MODEL** | A model supplied by an AI PROVIDER. |
| **MODELMODE** | One MODEL at one selected effort. It is the future MODEL ladder's unit, not a capability ranking. |

The human owner is an authority route, not a WORKER, CAPABILITY, or model selection. A WORKER may
change SESSION or MODELMODE without changing the DEPARTMENT that owns the WORKLOAD. A changed
MODELMODE is separately recorded for audit even when one SESSION continues.

`human` is a route, never a capability tier. It is unreachable by escalation, and no amount of
capability substitutes for it. Keeping it off the ladder is deliberate: on the ladder, a "next step
up" could land on it by arithmetic.

The ladder subsumes the existing route table rather than replacing it. `tool` is the old `tool`
route, `cheap` is the shipped `ab-low-helper`, `parent` is the session already running, and
`blocked` is `human`.

## Session introduction

Every user-visible response carries the Agent-base Board. It is deliberately a small ASCII
structure, not a decorative substitute for the answer: it makes the SESSION's temporary ROLE and
the current outcome owner visible before the prose.

```text
+-- AGENT-BASE ----------------------------------+
| ROLE: COORDINATOR                               |
| LEAD DEPARTMENT: MANAGEMENT                     |
| MODEL: GPT-5 at high | ELAPSED: 0m 25s          |
+-------------------------------------------------+
```

On its first response, a SESSION also says why it exists, what it is responsible for, and where
its authority stops. Without an explicit staffing assignment, it is the `COORDINATOR` in
`MANAGEMENT`: it works from the Roadmap, routes each substantive phase to one Lead DEPARTMENT,
consults only materially affected Departments, and may propose WORKERS. The human approves the
launch of every new WORKER. A dispatched SESSION states its assigned ROLE, DEPARTMENT, and optional
POSITION instead; it does not become a Department owner or coordinate further staffing.

The answer ends with the matching next-step block:

```text
+-- NEXT -----------------------------------------+
| <one roadmap-aware next step>                   |
+-------------------------------------------------+
```

## Order of decision

Authority, then eligibility, then economics. Every route is subject to all three, including `tool`
and the current session.

1. **Authority.** Credentials, a known or suspected vulnerability, authentication or authorization
   policy, personal data, or a new vendor, API, connector, account, or access path route to the
   human. This is ahead of everything, including determinism: a scripted edit to a credential file
   is still a credential decision, and the cheapness of the method changes nothing about that.
2. **Budgets.** An exhausted attempt, consultation, or escalation cap routes to the human, never one
   tier higher. Answering repeated failure with more money is the ratchet in its purest form.
3. **Mutation ownership.** No route may mutate the worktree without its lease, and a deterministic
   formatter is a writer like any other.
4. **Economics.** A deterministic tool if one suffices; otherwise the cheapest eligible tier.

## Selecting a WORKER

Two steps, and the order is what makes de-escalation real.

First choose the economical eligible tier for this WORKLOAD, from this WORKLOAD's own signals. Then prefer
reuse within that tier. Reuse of a stronger WORKER survives only as a bounded delegation-floor
exception, recorded on the decision's ledger line as an exception rather than described as
de-escalation.

A unit's required capability is never inherited from the previous unit. That alone is not enough:
with an idle expert and no standard WORKER, "reuse any sufficient idle WORKER" keeps the expert on
routine work while a test of the required tier passes. Tests therefore assert the WORKER that was
selected, not the tier that was asked for.

Below the delegation floor the current session does the work itself. A single obvious edit in a
known file is never delegated, because the handoff costs more than the change.

## One dispatch path

`plan` decides and records, the work is performed, `settle` confirms and closes. All three routes go
through one entry point, `dispatch`, so a deterministic tool that writes and a vendor session that
writes are settled by the same protocol and recorded the same way.

The two routes performed by the caller, a tool it runs and the session doing the work itself, still
hold the lease and are still recorded. Leaving the executor contract to each caller is what let the
first implementation reason about economics and then start a model anyway for work it had just
decided to do itself.

## Escalation

Escalation fires on named evidence only: two attempts on the same unit, the same verification
failure twice, ambiguous requirements, a blast radius crossing a component, an irreversible change,
uncertain concurrency behaviour, or a boundary others will follow being set. A hunch is not a signal.

Three counters, each gating a different act, and conflating them is how a budget cancels itself. An
**attempt** is a fresh dispatch of the unit and its cap decides whether the unit may be dispatched at
all. A **consultation** is a question asked of a stronger WORKER, and an **escalation level** is how
far up the ladder the unit has moved; those two gate the escalation, not the dispatch. Checking all
three before every dispatch let an escalation be authorized and then immediately cancelled, so the
documented path reached the expert and could never use it. A cap reads as "at most N": the Nth is
permitted and its answer is usable, the N+1th is refused.

All three are read from the run's own record, never from the caller's object. Counters kept only in
a caller's object survive exactly as long as that object, and a fresh unit after each resolved
failure reset every budget, which is how a cap meant to stop repeated spending stopped nothing.

Escalation asks a narrow question and does not transfer ownership of the unit. The stronger WORKER
receives the shared state, the named interfaces, and one question, and returns a decision with its
constraints. An advisory answer does not make an incapable owner capable: re-evaluate the remaining
work afterwards and reassign it if its requirements still exceed the owner.

## Boundaries that hold while work runs

- **One writer per worktree.** A single record keyed by the worktree, with acquire, release and
  reclaim serialized under a stable lock. Comparing a token and then unlinking a path is not atomic.
- **Uncertainty outlives its process.** When completion cannot be established, the worktree closes
  to further writers until a recorded operator intervention clears it. No universal signal proves
  an opaque CLI's descendants stopped writing, so none is claimed.
- **Recorded before launched.** An invocation is persisted before anything runs. A planned and
  unsettled invocation stays unresolved, which is the record a crash should leave.
- **Preparation is not execution.** Everything between taking the lease and returning a prepared
  invocation has launched nothing, so a failure in it is knowable: the lease goes back cleanly and
  any invocation already recorded is closed as failed. A stranded lease over work that never started
  is a worse outcome than the error that caused it. Anything that touches the worktree during
  preparation, including the runtime's own write probe, happens inside ownership.
- **A probe proves what it can reach, not what a WORKER can.** The write probe runs as the
  coordinator, so it establishes the coordinator's filesystem access and nothing about the WORKER's
  sandbox, approval policy, or tools. Those are separate facts with separate evidence.
- **Uncertain is a terminal state.** Not success, not failure, and not an invitation to retry
  automatically. A human-directed retry gets a fresh invocation identity and keeps the earlier
  outcome.
- **Create and resume state the same contract.** Where a vendor cannot restate a restriction on
  resume, a fresh restricted session is created instead. A session that could write is never reused
  as an advisory reader. `resume_verified` is false for both vendors, which is caution and not
  evidence: no operator here has demonstrated that a resumed session reapplies a tool allowlist, and
  nobody has demonstrated that it fails to. An earlier version of this page reported that as a
  measurement. It rested on a model's account of its own tools, which is the class of evidence this
  standard exists to distrust.
- **Read-only is enforced by permission, and narrowed again by configuration.** Measured
  2026-09-18 by attempting the calls rather than by reading a tool list: a read-only Claude WORKER
  was *offered* the operator's external tool servers and its call was denied by `dontAsk`, and a
  read-only Codex WORKER was offered `apply_patch` and its write was blocked by the sandbox. The
  boundary held in both. Excluding those servers outright is nevertheless carried as defence in
  depth, so a WORKER cannot attempt what it has no business attempting and the denial never has to
  be the only thing standing between it and the world.
- **Orchestration is constructed, not inherited.** One WORKER per run may dispatch, checked against
  the run's own record. A child context is built with the authority switched off, and a dispatched
  invocation may not bootstrap a new run. The boundary is cooperative: a value in the environment of
  a process the user controls orders agent-base's own executions and is not a barrier against
  hostile code running as that user.

## Shared task state

Persistent WORKERS do not share a conversation, and keeping them in step by copying every message to
everyone would spend more than the arrangement saves. What a joining WORKER receives is the shared
state, its own work unit, and named files. Never a transcript, never another WORKER's context.

Writing to that state is gated on a named boundary: a decision made, a constraint discovered, a unit
completed, a hypothesis rejected, an escalation triggered, a verification failed, or a phase change.
An entry that cannot name the moment that made it worth writing did not need writing, and a state
file anyone may append to at any time becomes a transcript within an afternoon.

Live state is untracked operational memory with a defined end. At a handoff it is distilled into an
ordinary `.agents/sessions/` snapshot under `AB-SESSION-001`, which is already portable across
machines and vendors and already reviewed in Git. There is no second durable store.

The distillation drops everything that could act: lease tokens, ownership tokens, and provider
handles. A snapshot is intent, and intent that silently resurrects a lock or a live vendor session
on another machine is a trap rather than a handoff. Unresolved invocations travel with it, because a
handoff whose reader cannot see what nobody observed reads as completion.

## Activation

Two channels, because neither is sufficient alone.

`AB-MODEL-001` carries the order of decision inline rather than a pointer, so it reaches every
vendor including the two with no hook capability.

A `UserPromptSubmit` hook carries the second channel for Claude and Codex, and it is deliberately
narrower than a staffing reminder. A prompt hook cannot observe a work-unit boundary: one prompt can
grow into several units with no further submission, and a condition broad enough to catch that would
fire on ordinary prompts until the reader learned to skip it. It therefore surfaces only the two
states that survive between sessions and that nothing else brings back up: a held lease whose
invocation never recorded an outcome, and an uncertain writer that closes the worktree. Both are
recovery states, and silence is correct the rest of the time. The hook stays quiet inside a
dispatched invocation, since a child is not the coordinator and reminding it to recover would invite
the recursion the invocation contract exists to prevent.

The authoritative check stays at work-unit registration in the procedure. The hook is a reminder,
never the enforcement.

## One checker, not one builder

Four places in this repository build a vendor command line: the two fleet launch gates, the
Executor/Reviewer loop, and the staffing adapter. They differ for real reasons. A fleet writer runs
against a trusted client root where `--permission-mode auto` reaches a reviewer; a staffing writer is
non-interactive and under `auto` would sit there asking nobody. The loop's Reviewer keeps Bash
because its findings rest on checks it runs. Forcing one command shape would flatten distinctions
that earned their place.

What drifted, four separate times in one day, was never the shape. It was a small set of facts any
launcher can get wrong alone: a sandbox bypass reaching a read-only auditor, two Codex flags that
refuse each other, a permission mode that waits for a human who is not there, and an advisory role
offered the operator's whole external tool inventory.

So `vendor_launch.py` is a checker. Each launcher composes its own command and submits it before
running. `tests/test-vendor-launch-invariants.py` enumerates every launcher and every role and asks
the same questions of each, which is the instrument that would have caught all four the same
morning. A launcher missing from that table is a launcher nothing holds.

## Configuration

Which model answers a capability is operator-local, at
`${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/staffing.json`, beside `vendor-support.json`. An
unmapped tier is a refusal, never a substitution: borrowing a neighbouring tier spends a model the
operator never chose. A missing file is not an error and degrades to the session already running
plus the vendor's native helper, which is exactly what agent-base did before this existed.

`resume_verified` is false for every vendor until an operator demonstrates that its resume path
restates an invocation contract.

## Verification and evidence

A result arrives in an addressed envelope or it is not a result. A non-zero exit, a missing or
misaddressed envelope, and a success claimed over content the worktree has moved past all resolve to
uncertain. Prose is preserved for reasoning and never read as control flow.

A result is measured against the content it was produced from, and that content is identified down
to the bytes. HEAD alone cannot tell a moved worktree from a still one, and `git status` alone names
only *which* paths differ, not how: two successive edits to an already-modified file produce
identical status lines. The changed paths are therefore read, not merely listed.

Each invocation observes its own baseline, after ownership where there is any. Inheriting the
coordinator's made every later unit measure against the state of the worktree when the run began,
which stops being true the moment any earlier unit writes.

Three observations, kept apart because they answer different questions and can disagree:

| Observation | Question | If absent |
| --- | --- | --- |
| **Claim** | What did the WORKER say about its own work? | No result at all; uncertain |
| **Verification** | What did something else establish about that work? | `unverified`, not success |
| **Quiescence** | Did writing stop? | The worktree closes to further writers |

`unverified` is a terminal outcome of its own. It is not success, because a WORKER reporting that it
finished is the same class of evidence as a WORKER reporting that its tests passed. It is not
uncertainty either. It is a statement about the *task*: verification is absent. What happened to the
worktree is recorded separately, by execution and quiescence, and an unverified task can sit beside
a worktree that is still closed. Reporting one without the other hides a recovery obligation. Collapsing it into success is how a no-op writer settles clean; collapsing it into
uncertainty would close the tree over work that is probably fine.

Quiescence is never supplied on a WORKER's behalf, and a normal return is not a promise about it.
Returning zero says this call finished; it says nothing about what the call may have left running,
and a callback that starts a writer in the background satisfies the first while violating the
second. So the level is reported by whoever can establish it, or it is unestablished. A `perform`
callback states what it observed. A launched WORKER is given the no-background-writes obligation in
its prompt and answers it in its result envelope; only an explicit acknowledgement establishes
anything, capped at what the route could offer. Silence, a reported writer, and an unrecognized
answer all establish nothing, and the uncertain record keeps them apart, because one names a writer
somebody can go and look for, one says the obligation went unanswered, and one points at a protocol
mismatch rather than at the WORKER.

**What the cooperative level is not.** It is an acknowledgement, not an observation. It does not
prove that nothing is still writing: a WORKER that answers dishonestly, or a tool inside it that
keeps writing after the WORKER believes it has stopped, both produce a cooperative release over a
worktree that is still moving. Nothing here detects that, and nothing here should be described as
if it did. What the protocol buys is narrower and still worth having: nobody is credited with a
promise they were never asked for, and the level recorded is one somebody actually gave. Where that
is not enough, the answer is a mechanically observed completion, which means waiting for the writer
yourself on a route where you can.

## What a staffed run proves about cost, and what it does not

A run that completes correctly is evidence about correctness and says nothing about economy. Three
things stand between the receipts and a claim of saving, and all three should be stated whenever the
question is asked.

**A cheaper selected model is not a saving.** It is a cheaper line item. Staffing adds handoff,
briefing, verification, and retries, and a unit routed to a cheap WORKER that fails twice and lands
on the human costs more than the same unit done once by the session already running.

**The coordinator's own cost is not measured.** Receipts record what each dispatched invocation
spent, because vendors report it. Nothing reports what the coordinating session spent deciding,
briefing and settling, and that is exactly the overhead staffing adds. The measurable half is the
half that flatters it.

**There is no counterfactual.** The same work cannot honestly be run twice, because the second
attempt benefits from the first. A comparison therefore needs either matched units across a long
enough period to average out their differences, or an explicit decision to compare something else
and say so.

None of this makes the question unanswerable. It makes a confident short answer wrong, and a
staffed run reported as a saving on the strength of its model choice alone is the exact mistake this
section exists to stop.

Receipts stay content-free: vendor, role, capability, digested identifiers, and observed counts,
never the task, prompt, paths, transcript, or a raw vendor event stream. An invocation that started
and never stopped is reported as unmatched; a stop is never manufactured to make counts pair.

## Deliberately not in v1

Learned or adaptive routing. Currency and quota budgets. External gateways and local models. True
parallel writers, for which `codex exec --worktree` is the recorded path. Self-reported confidence.
Automatic retirement of idle WORKERS. Cross-machine orchestration. Migrating the two fleet launch
gates and the Executor/Reviewer loop onto the shared adapter.
