#!/usr/bin/env python3
"""The result envelope for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Success is a claim that must arrive intact, addressed to this invocation, and be believed only
    as far as an independent check confirms it.

Nothing here infers a result from prose. The Executor/Reviewer loop's tolerant parser is the worked
example of why: it read a findings section with no structured blocks as agreement, so a reviewer's
plain-sentence objection was reported to the human as an approval. Control flow that guesses is
control flow that eventually guesses wrong in the direction of "everything is fine", because that is
the branch an absence falls into.

Both Primary vendors can emit a schema-shaped result today, so the envelope is asked for rather than
reconstructed. What a vendor wraps around it is translated at this boundary, and anything that does
not yield a complete, matching envelope is an unresolved outcome rather than a failure of nerve.
"""

from __future__ import annotations

import json
from typing import Any, NamedTuple

ENVELOPE = "ab-result-envelope-v1"

# What an Actor may claim about its own work. Deliberately not the same vocabulary as the run's
# terminal outcomes: an Actor reports what it did, and the runtime decides what that means.
CLAIM_COMPLETED = "completed"
CLAIM_FAILED = "failed"
CLAIM_BLOCKED = "blocked"
CLAIMS = (CLAIM_COMPLETED, CLAIM_FAILED, CLAIM_BLOCKED)

# What an Actor states about what it left running. Separate from `claim`, which is about the work:
# an Actor can finish the task correctly and still have started something that outlives it, and
# those are the two facts the lease and the outcome are decided from.
#
# `quiet` is an acknowledgement rather than an observation. The Actor was told, in its prompt, not
# to leave anything writing, and this is where it says it complied. A CLI that returns zero has not
# said that, which is why this field exists and why absence is never read as agreement: the runtime
# used to assign the cooperative level by constant, before the invocation had run at all.
COMPLETION_QUIET = "quiet"
COMPLETION_LEFT_RUNNING = "left_running"
COMPLETION_UNKNOWN = "unknown"
COMPLETIONS = (COMPLETION_QUIET, COMPLETION_LEFT_RUNNING, COMPLETION_UNKNOWN)

# Handed to an Actor verbatim in its prompt. The obligation has to reach the participant, or the
# acknowledgement is about nothing.
NO_BACKGROUND_WRITES = (
    "Before you finish: everything you start must finish before you return. Do not leave a "
    "background process, a detached command, a watcher, or a scheduled job writing to this "
    "worktree after your final message. If you cannot guarantee that, set completion to "
    f"{COMPLETION_LEFT_RUNNING!r} and say what is still running; the worktree is then held for a "
    "human instead of being handed to the next Actor."
)

# `invocation_id` is not here. Addressing is still checked when it is present and a wrong one is
# still refused, but an *absent* identifier is an omission rather than a misdirection, and refusing
# it threw away correct work on this feature's first real task: an Actor wrote a good file, dropped
# one field, and the runtime established nothing, held the worktree and stopped the next unit.
#
# What the check defends against survives, but the reason is narrower than "one invocation at a
# time". That rules out simultaneous ambiguity and not a stale result from an *earlier attempt* on
# the same unit. What makes this acceptable is the transport: the runtime reads this text out of
# the subprocess it started for this exact Plan, so the response is bound to the request whether or
# not the Actor addressed it.
#
# That binding is the whole licence, and it does not travel. Any transport where a reply arrives
# separately from its request - a correspondence, a queue, an imported transcript, a resumed
# session - must address by invocation, because nothing there ties the answer to the attempt. The
# content revision does not supply that tie: it describes the worktree, not the request.
REQUIRED_FIELDS = ("envelope", "unit_id", "claim")


class EnvelopeError(RuntimeError):
    """Every refusal here. None of them resolves to success."""


class Envelope(NamedTuple):
    invocation_id: str
    unit_id: str
    claim: str
    evidence: str
    narrative: str
    content_revision: str | None
    # Whether the Actor named the invocation it was answering. False means it named only the unit,
    # which reaches one invocation here and is weaker addressing than the protocol asks for.
    addressed_by_invocation: bool
    # Exactly what the Actor wrote, or None when it wrote nothing. Deliberately not normalised into
    # the known set: only `quiet` means anything to the runtime, and everything else establishes
    # nothing whether it is `left_running`, an invented word, or absent. A normalising step here
    # collapsed those into one value, which made the raw answer unrecoverable and, being read by
    # nobody, could be deleted without a single test noticing. The distinctions that are worth
    # keeping are read from this field where they are actually used.
    completion: str | None

    @property
    def claims_success(self) -> bool:
        return self.claim == CLAIM_COMPLETED


def _candidates(stdout: str) -> list[dict[str, Any]]:
    """Every JSON object in the output that announces itself as one of ours.

    Vendors wrap their own envelopes around a result, and a stream can carry many objects. Scanning
    line by line and demanding our own marker is what keeps a vendor's framing from being mistaken
    for a result, and vice versa.
    """
    found = []
    for line in stdout.splitlines():
        line = line.strip()
        if not line.startswith("{"):
            continue
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict) and value.get("envelope") == ENVELOPE:
            found.append(value)
    return found


def parse(stdout: str, *, invocation_id: str, unit_id: str) -> Envelope:
    """Read the result this invocation produced, or refuse with the reason.

    Addressing is checked, not assumed. A result carrying another invocation's identifiers is a
    stronger signal of confusion than of success, and accepting it would let a stale or misrouted
    reply close work it never touched.
    """
    candidates = _candidates(stdout)
    if not candidates:
        raise EnvelopeError(
            "no result envelope in the output; a run that produced no addressed result has an "
            "unknown outcome, which is not the same as a failed one"
        )
    if len(candidates) > 1:
        raise EnvelopeError(
            f"{len(candidates)} result envelopes in one output; which one describes this "
            "invocation cannot be decided by picking the last"
        )
    value = candidates[0]
    missing = [field for field in REQUIRED_FIELDS if not value.get(field)]
    if missing:
        raise EnvelopeError(f"result envelope is missing {missing}")
    addressed = value.get("invocation_id")
    if addressed and addressed != invocation_id:
        raise EnvelopeError(
            f"result envelope is addressed to invocation {addressed}, not {invocation_id}"
        )
    if value["unit_id"] != unit_id:
        raise EnvelopeError(
            f"result envelope names unit {value['unit_id']}, not {unit_id}"
        )
    if value["claim"] not in CLAIMS:
        raise EnvelopeError(f"claim must be one of {CLAIMS}, not {value['claim']!r}")
    completion = value.get("completion")
    return Envelope(
        invocation_id=addressed or invocation_id, unit_id=value["unit_id"], claim=value["claim"],
        addressed_by_invocation=bool(addressed),
        evidence=str(value.get("evidence", "")), narrative=str(value.get("narrative", "")),
        content_revision=value.get("content_revision"),
        completion=str(completion) if completion is not None else None,
    )


def template(invocation_id: str, unit_id: str) -> str:
    """The exact shape an Actor is told to emit. Handed to it in its prompt, not described."""
    return json.dumps({
        "envelope": ENVELOPE,
        "invocation_id": invocation_id,
        "unit_id": unit_id,
        "claim": f"one of {list(CLAIMS)}",
        "evidence": "commands run and their results, not a restatement of the task",
        "narrative": "optional reasoning; never read as control flow",
        "content_revision": "the value supplied in this invocation's contract",
        "completion": f"one of {list(COMPLETIONS)}; see the obligation above",
    }, sort_keys=True)
