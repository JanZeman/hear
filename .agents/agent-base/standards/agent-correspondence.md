# Agent Correspondence

Several WORKERS working one problem by writing to each other in addressed, dated, immutable
messages, with a human deciding when each one speaks.

It exists because two independent agents found defects that neither found alone: on roadmap item 148
(Staff work units with the cheapest sufficiently capable WORKER) a reviewing agent raised four
blocking architecture findings before implementation started, nine more against the implementation,
and several against repairs made an hour earlier that the implementer's own passing tests had walked
straight past. The cost of the second agent was lower than the cost of finding any one of those
later.

Use it for work that is expensive to get wrong: architecture, contracts others depend on, security
and safety boundaries, anything hard to reverse. Do not use it for ordinary changes; two agents
corresponding about a typo is theatre.

## Correspondents are addressed by DEPARTMENT

The address is a **DEPARTMENT**, never a MODEL or SESSION nickname. A MODEL can be replaced and a
SESSION exists only for its own lifetime; neither survives the subject a correspondence addresses.
A message runs from Engineering to Architecture the way a memo runs between departments of a firm,
and the DEPARTMENT that owns a subject is the DEPARTMENT that should answer about it. The catalogue
is `.agents/agent-base/departments/_DEPARTMENTS_CATALOG.md`.

`all` addresses every correspondent.

A message records its sending WORKER separately from the addressed DEPARTMENT. A POSITION is
optional and names a recurring specialization such as Flutter Engineer; several WORKERS may hold
one POSITION. A ROLE is the sender's temporary function in this WORKLOAD, such as executor or
reviewer. Neither WORKER, POSITION, nor ROLE belongs in the address.

When several WORKERS belong to one DEPARTMENT, the human relay or the Venture coordinator chooses
which one answers. Agent Correspondence does not yet automate that assignment.

## Opening one

```bash
python3 scripts/correspondence.py open --as <your Department> --with <theirs> \
    --topic "<what this is about>" --because "<why these Departments, in one line>"
```

That creates the directory and the roster and prints an invitation: the text to paste into a second
window, complete and needing no editing. The other SESSION runs `join --as <Department>`, which
records its WORKER and says what is waiting.

The human opens the second window. That is the boundary rather than a gap: the relay is the human,
and an agent that could start another agent is a different feature with different risks. Everything
either side of that one manual act is prepared for them.

DEPARTMENTS are validated against the catalogue. An invented DEPARTMENT is refused, and a repository
with no catalogue is told so rather than given a guess. Replacing a recorded WORKER needs
`--take-over`, because that is the human's decision and not the joining SESSION's.

## Where it lives and how a message is named

`.agents/correspondence/`, beside `sessions`, `roadmap` and `fixes`.

```
YYMMDD_HHMM_<sender>_to_<recipients>_<topic>.md
260919_0836_architecture_to_engineering_repairs-review.md
260919_1104_engineering_to_architecture+quality_lease-protocol.md
```

Recipients are joined with `+`. The addressing is in the filename rather than only inside the file
because addressing that only a parser can see is addressing the human relaying the messages cannot
check.

Two files are not messages: `CURRENT_STATE.md`, the one file updated in place, owned by whoever
leads the work; and `ROSTER.md`, which records the current WORKER for each DEPARTMENT.

## What a message contains

A heading that states the finding rather than the subject, then `From Department`, `To Department`,
`From Worker`, optional `Position`, `Role`, `Session`, `Runtime`, `AI Provider`, `Model`, and
`Modelmode`, followed by `Phase`, `Status`, and the repository state the sender actually inspected,
by commit. Then the substance, and last a `Verification / Evidence` section saying what the sender
ran and what it did not run. A review that does not say what it failed to check is a review whose
silence reads as coverage.

For example:

```text
From Department: Engineering
To Department: Architecture
From Worker: current engineering worker
Position: Flutter Engineer
Role: executor
Session: current Codex session
Runtime: Codex
AI Provider: OpenAI
Model: GPT-6 Astra
Modelmode: GPT-6 Astra at high effort
```

**Messages are immutable.** A sent message is never edited, including to fix something it got wrong;
the correction is the next message. An unsent message may still be corrected, because sending wrong
instructions is worse than the record being tidy. This is the same discipline `AV-SESSION-001`
applies to session snapshots and for the same reason: a record that can be revised cannot be cited.

## Send it, then say so

Write the reply yourself and send it; do not wait for a look first. Tell the human what went out,
name the file, and say plainly that they can send a correction as the next message if something was
misread. That is one notice instead of one round trip.

This is not a softening of independence. The point of a second correspondent is a view that was not
steered, so the draft was always written and sent unprompted; what changed is only when the human
sees it. A message is immutable either way, so a pre-send look and a post-send correction differ
only in whether a miscommunication costs one letter or two. The human who set this default is
trading a round trip on every message for an extra letter on the rare one that needs it, having
said yes to the look often enough that the look stopped paying for itself.

Ask before sending, instead, only when the human has said to for that specific correspondence or
that specific kind of message. Absent that, the default is: send, then tell.

## Whose turn it is

Derived from the directory, never remembered by either side:

```bash
python3 scripts/correspondence-turn.py --as <the DEPARTMENT you hold>
```

The rule is the newest message addressed to me, unless I have already answered its sender. That is
identical to "the newest message decides" while there are two correspondents, and correct when a
third joins: a message I sent to somebody else is not an answer to this one. It also gives the
repeated-signal guard for free, so a second wake-up with nothing new says "nothing for you" instead
of answering twice. An unknown role is reported as an unknown role and not as a quiet turn, because
a mistyped role otherwise reads as permission to stop.

`AV-INPUT-001` binds a bare `r` to running it and reacting. The human types one character and does
not have to know whose turn it is.

## What this is not

It is **not** the durable record. `.agents/sessions/` carries durable intent under `AV-SESSION-001`,
and that is what a later SESSION or another RUNTIME reads first. A correspondence is the working
record of how a decision was reached: kept in Git because the decisions cite it, read on demand
rather than loaded at session start, and never part of always-on context. It accumulates and is
never pruned, so treat it the way `_ROADMAP_HISTORY.md` is treated.

Nothing that can act crosses from a correspondence into a snapshot: no tokens, no AI PROVIDER
handles, no live SESSION identifiers.

It is not a transcript. What keeps it a summary is the cost of a message: a human has to relay it,
and each one is addressed, dated, and permanent. Do not narrate progress, do not acknowledge receipt
in a message of its own, and do not write when nothing has changed. A correspondence of thirty
messages in a day is one that stopped being worth its transport.

Explicitly out of scope until there is evidence for it: WORKERS waking each other, any daemon, and
any automatic invocation of a paid AI PROVIDER SESSION. The human holding the relay is the human holding
the cost.

`AV-SAFE-001` applies. Messages quote code, paths and test output into tracked history.
