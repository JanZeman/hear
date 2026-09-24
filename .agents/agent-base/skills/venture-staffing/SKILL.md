---
name: venture-staffing
description: Staff a coherent work unit with the cheapest sufficiently capable Actor, and hold the boundaries that keep that safe. Use when work spans more than one coherent unit or more than one Actor, when deciding whether to delegate at all, when a unit has failed twice and escalation is in question, when a worktree refuses a writer, or when an invocation's effects are unknown after a crash. A single bounded hand-off needs only `model-routing.md`, not this.
---

# Venture Staffing

`model-routing.md` covers one bounded hand-off. This covers work that spans several units or several
Actors, where the expensive mistakes are no longer about which model but about who may write, what
was recorded before it ran, and whether anyone observed the result.

Read `.agents/agent-base/standards/venture-staffing.md` for the contract. This file is the
procedure.

## Before staffing anything

Answer three questions in this order, and do not reorder them for convenience.

1. **Is this the human's decision?** Credentials, a vulnerability, authentication or authorization,
   personal data, or a new external access path. Determinism does not exempt it: a scripted edit to
   a credential file is a credential decision.
2. **Is there budget left?** An exhausted attempt, consultation, or escalation cap goes to the
   human, never one tier up.
3. **Does this mutate the worktree, and is its lease free?** A formatter counts.

Only then ask what it costs.

## Deciding who does it

```bash
python3 scripts/staffing_lease.py status --root .
```

A `free` worktree may be written to. An `uncertain` one may not, by anyone, until a recorded
intervention clears it; see recovery below.

Then, in order:

- A deterministic tool if one produces the result. No model is cheaper than no model.
- Below the delegation floor, do it yourself. One obvious edit in a known file costs more to hand
  over than to make; routing names the current session and `dispatch` runs no model for it.
- Otherwise the cheapest tier this unit's own signals justify, reusing an idle Actor at that tier.
  Do not reach for a stronger idle Actor because it is there.

State the choice in one line before acting: the unit, the capability, and why.

## Dispatching it

One entry point, whichever route the unit takes:

**One command, from the worktree you are working in.** Nothing below needs Python.

```bash
python3 scripts/staffing.py plan --goal "<what this Actor is to achieve>" --files a.py
python3 scripts/staffing.py run  --goal "..." --files a.py --mutates --verify "pytest -q"
python3 scripts/staffing.py status
python3 scripts/staffing.py finish  --invocation <id> --completion observed --evidence "..."
python3 scripts/staffing.py recover --evidence "<what you established>" --finding failed
```

A mutating unit that routes back to you does **not** mean "go ahead". The command takes the
worktree lease on your behalf, records the invocation, and hands you a token; `finish` is how you
give it back. Until then nothing else may write there, which is the whole point and is what a bare
"do it yourself" quietly skipped.

Two things about the calling session have to be true for any of this. It needs an identity, so a
ledger it opened can be told from a ledger somebody else's process is adopting: `AB_COORDINATOR_ID`,
or the vendor's own session variable. And it needs to say which model it is, `AB_COORDINATOR_MODEL`,
or its capability is unknown, it is not offered as a candidate, and the delegation floor cannot fire.
Neither is guessed at.

`plan` answers who would do it and what would run, and creates nothing: asking used to require
starting a run, and a run takes ownership and refuses to be started twice. `--mutates` is what takes
the worktree lease. `--verify` is a shell command; without one the unit settles `unverified`, which
is honest rather than failed. `run` exits non-zero on anything but success or unverified.

A held worktree is not an error to work around. It means something may still be writing there:
establish otherwise, then `recover` with what you established.

The Python interface is the same thing without the argument parsing:

```python
staffing_runtime.dispatch(session, unit, actors,
                          unit_goal="<what this Actor is to achieve>",
                          files=("<named file>",),
                          perform=<callable, for tool and parent routes>)
```

It plans, takes the lease when the unit mutates, records the invocation before anything runs, and
settles from what it observed. Three routes reach it and they are not the same act:

| Route | Who performs it | What you supply |
| --- | --- | --- |
| `tool` | you, running the tool | `perform`, returning a `Performed` |
| `parent` | you, doing the work yourself | the same |
| CLI Actor | the runtime, starting a vendor session | nothing; it builds the prompt and reads the result |

The two caller-performed routes still hold the lease and are still recorded, because a formatter
that writes and a vendor session that writes are the same kind of event. `perform` is required for
them and its absence is refused rather than quietly turned into a launch.

**Say what you established about writing, or the worktree stays closed.**

```python
from staffing_runtime import Performed
from staffing_lease import COMPLETION_OBSERVED

def perform(prepared):
    finished = subprocess.run([...], check=False)      # waited for; the process is gone
    return Performed(finished.returncode, "formatter rewrote 3 files", COMPLETION_OBSERVED)
```

A bare `(exit_code, evidence)` pair is still accepted and establishes nothing, so a mutating unit
settles with its worktree held for a human. That is deliberate and it is not a bug to work around:
returning normally says this call finished, not that nothing you started is still writing, and those
are different facts. Claim `COMPLETION_OBSERVED` only when you waited for the writer and it is gone.
Claim `COMPLETION_COOPERATIVE` when you are relying on a promise rather than on having watched.
Claim neither when you do not know.

Cooperative is a promise somebody made, not a fact somebody checked. It does not survive an Actor
that answers dishonestly or a tool that keeps writing after the Actor thinks it stopped, and nothing
in the runtime detects either. Do not report a cooperative release as proof the worktree is quiet.

The CLI route asks its Actor the same question in the prompt and reads the answer from the
`completion` field of the result envelope. An Actor that exits cleanly without answering establishes
nothing either; the runtime never supplies that answer on the Actor's behalf.

A finished task and a closed worktree are separate results. A unit can settle `succeeded` while its
worktree is still held, and reporting the first without the second hides a recovery obligation that
is now somebody's to discharge.

Do not assemble a launch yourself from `plan`. That is how the first version of this ran: every
caller invented its own idea of which prompt an Actor receives, how a vendor's output is translated,
and when the provider handle is captured, and none of it was reachable by a test.

## Saying who checked it

`dispatch` takes `verify=`, and without it every route settles as `unverified`. That is the honest
state and not a failure: the work may well be done, and nobody looked.

Supply the cheapest check that could actually fail. A test run, a lint, a `git diff` you read, a
grep for the thing that was supposed to appear. An Actor's own report that it succeeded is not one
of these, which is the entire reason this argument exists separately from the result it judges.

When an Actor claims completion and your check disagrees, that is a failure. Its confidence is not
evidence against your check.

## Escalating

Only on named evidence: two attempts, the same verification failure twice, ambiguous requirements,
a blast radius crossing a component, an irreversible change, uncertain concurrency behaviour, or a
boundary others will follow. Write down which one fired.

Escalate through `staffing_runtime.escalate(session, unit)`, not by editing counters on your own
unit. It records the consultation where the next decision will read it; a counter you keep yourself
lasts exactly as long as you remember to pass it along.

Ask one narrow question. Send the shared state, the named interfaces, and the question, never a
transcript. Record the answer and its constraints where the next Actor will read them.

Then return. The expert answered a question; it did not take the unit. If the remaining work still
exceeds the owner's capability, say so and reassign it rather than letting the expensive Actor drift
into ordinary execution.

## When something did not finish

A crash, a timeout, a lost stream, or a non-zero exit leaves an invocation whose effects nobody
observed. That is `uncertain`, which is neither success nor failure.

```bash
python3 scripts/staffing_run.py unresolved <run-id>
```

Do not retry it automatically. Reconcile first: look at the worktree, establish what actually
landed, and record that with evidence. Only then may the unit be attempted again, with a fresh
invocation identity; the earlier outcome stays in the record rather than being overwritten.

If the worktree's lease is `uncertain`, the writer may still be running. Stop it, observe that it
stopped, and record what you did. A confirmation with no evidence overrides the guarantee instead of
meeting it, which is why the tooling asks for the evidence and not for a yes.

## What this never does

- Launch a writer without the lease, whatever the route.
- Treat a missing result as a good one.
- Answer repeated failure with a more expensive model.
- Let a dispatched Actor staff work of its own, or start a run to get around that.
- Claim that an opaque CLI's descendants have stopped writing.

## Reporting

Say which capability did the work and why, name any escalation and what evidence triggered it, and
report observed cost where a vendor reports it and "unknown" where it does not. A confident number
nobody measured is worse than an admitted gap.
