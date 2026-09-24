#!/usr/bin/env python3
"""Deciding who does a work unit, for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Authority, then eligibility, then economics. A route is only ever chosen from what is already
    permitted, and the cheapest sufficient option is chosen among those, never before them.

That ordering is the whole design, and getting it wrong is subtle. An earlier draft put "a
deterministic tool can do this" first, as a pure economic shortcut, which quietly let a scripted
operation on a credential file skip the human route entirely. Determinism is a property of the work,
not a permission, so it decides *which* permitted route is cheapest and never whether one is
permitted.

The second thing this module exists to get right is de-escalation, and the first attempt at it was
wrong in a way that would have passed its own test. Never inheriting a unit's required capability is
necessary and not sufficient: with an idle expert available and no standard Actor, reuse still picks
the expert, the required tier still reads "standard", and the assertion passes while the expensive
behaviour continues. Selection is therefore two steps, tier before Actor, and the tests assert the
Actor that was selected rather than the tier that was required.
"""

from __future__ import annotations

from typing import Any, NamedTuple

# The vendor-neutral ladder. `TOOL` is on it because "no model at all" is genuinely the cheapest
# sufficient capability for some work, not a separate mechanism.
TOOL = "tool"
CHEAP = "cheap"
STANDARD = "standard"
STRONG = "strong"
EXPERT = "expert"
LADDER = (TOOL, CHEAP, STANDARD, STRONG, EXPERT)

# `human` is a route, never a tier. It is not reachable by escalation and no amount of capability
# substitutes for it, which is exactly why it must not sit on the ladder where a "next step up"
# could land on it by arithmetic.
ROUTE_HUMAN = "human"
ROUTE_TOOL = "tool"
ROUTE_ACTOR = "actor"

# Work whose handling is an authority question rather than a difficulty question. This list is the
# existing `blocked` route from `model-routing.md`, preserved verbatim in meaning: the source design
# note's ladder omitted it, and adopting that omission would have been a silent safety regression.
SENSITIVE_CLASSES = (
    "credentials",
    "vulnerability",
    "authentication",
    "authorization",
    "personal_data",
    "new_external_access",
)

DEFAULT_LIMITS = {
    "max_attempts_per_unit": 2,
    "max_consultations_per_unit": 2,
    # Two levels so the documented standard -> strong -> expert path is actually reachable. A cap of
    # one would advertise a recovery route that the configuration forbids.
    "max_escalation_levels": 2,
    "delegation_floor_files": 2,
}


class RoutingError(RuntimeError):
    """Every refusal here. All of them fail closed."""


class Unit(NamedTuple):
    """What is known about a work unit before anyone is staffed to it."""

    unit_id: str
    # What this unit is, in a few words, for whoever reads the ledger later. Not a routing signal
    # and deliberately not consulted by any decision here: an identifier answers "which unit", and
    # a person reading `U085722 succeeded sonnet` a week later needs the other question answered.
    label: str | None = None
    sensitive_classes: tuple[str, ...] = ()
    mutates: bool = False
    deterministic_tool: str | None = None
    files_touched: int = 1
    components_touched: int = 1
    fully_specified: bool = True
    mechanical: bool = True
    reversible: bool = True
    sets_boundary: bool = False
    concurrency_sensitive: bool = False
    attempts: int = 0
    consultations: int = 0
    escalation_level: int = 0
    repeated_identical_failure: bool = False


class Actor(NamedTuple):
    actor_id: str
    capability: str
    may_write: bool
    idle: bool = True
    is_parent: bool = False


class Decision(NamedTuple):
    route: str
    capability: str | None
    actor_id: str | None
    reason: str
    exception: str | None = None

    def ledger_line(self, unit_id: str) -> str:
        """One auditable line per decision. Routing that cannot be read back is not auditable."""
        target = self.actor_id or self.capability or "-"
        suffix = f" [exception: {self.exception}]" if self.exception else ""
        return f"{unit_id}\t{self.route}\t{target}\t{self.reason}{suffix}"


def rank(capability: str) -> int:
    try:
        return LADDER.index(capability)
    except ValueError:
        raise RoutingError(f"{capability!r} is not on the capability ladder {LADDER}")


def required_capability(unit: Unit) -> str:
    """The minimum capability this unit's own signals justify.

    Computed only from this unit. It is never carried over from whatever the previous unit needed,
    which is the mechanical half of the anti-ratchet: an expensive decision does not make the next
    piece of routine execution expensive.
    """
    if unit.sets_boundary or unit.concurrency_sensitive or not unit.reversible:
        # Architecture's own `Leads` clause names this class: a boundary, a dependency direction, a
        # contract others follow, or a commitment that is expensive to reverse. It is a reason to
        # look at consequence, not a rule that every Architecture-led unit needs the top tier, so it
        # sets a floor of `strong` and lets the remaining signals decide whether `expert` is earned.
        base = STRONG
    elif not unit.fully_specified or unit.components_touched > 1:
        base = STRONG
    elif unit.mechanical and unit.fully_specified:
        base = CHEAP
    else:
        base = STANDARD
    if unit.repeated_identical_failure or unit.attempts >= 2:
        base = LADDER[min(rank(base) + 1, len(LADDER) - 1)]
    return LADDER[min(rank(base) + unit.escalation_level, len(LADDER) - 1)]


def escalation_evidence(unit: Unit) -> list[str]:
    """The named signals that justify moving up a tier. Intuition is not on this list."""
    evidence = []
    if unit.attempts >= 2:
        evidence.append("two attempts on the same unit")
    if unit.repeated_identical_failure:
        evidence.append("the same verification failure twice")
    if not unit.fully_specified:
        evidence.append("requirements still ambiguous")
    if unit.components_touched > 1:
        evidence.append("blast radius crosses a component")
    if not unit.reversible:
        evidence.append("the change is expensive to reverse")
    if unit.concurrency_sensitive:
        evidence.append("concurrency or lifetime behaviour is uncertain")
    if unit.sets_boundary:
        evidence.append("a boundary or contract others follow is being set")
    return evidence


def caps_exhausted(unit: Unit, limits: dict[str, int]) -> str | None:
    """Whether this unit may be dispatched again at all.

    Only the attempt cap belongs here, and the first version's mistake was including the other two.
    Each counter gates a different act: an attempt is a fresh dispatch of the unit, a consultation is
    a question asked of a stronger Actor, and an escalation level is how far up the ladder the unit
    has moved. Checking all three before every dispatch meant `escalate` could authorize a second
    consultation and `route` would immediately cancel it, so the documented standard to strong to
    expert path reached the expert and could never use it.

    A cap that is reached sends the work to the human rather than to a more expensive Actor.
    Answering repeated failure with more money is the ratchet in its purest form.
    """
    if unit.attempts >= limits["max_attempts_per_unit"]:
        return (f"attempt cap reached ({unit.attempts} of "
                f"{limits['max_attempts_per_unit']} dispatches used)")
    return None


def escalation_refused(unit: Unit, limits: dict[str, int]) -> str | None:
    """Whether another question may be asked of a stronger Actor. The escalation's own budgets.

    Read as "at most N", so the Nth is permitted and its answer is usable; the N+1th is refused.
    """
    if unit.consultations >= limits["max_consultations_per_unit"]:
        return (f"consultation cap reached ({unit.consultations} of "
                f"{limits['max_consultations_per_unit']} asked)")
    if unit.escalation_level >= limits["max_escalation_levels"]:
        return f"escalation cap reached (level {unit.escalation_level})"
    return None


def _cheapest_eligible_tier(unit: Unit) -> str:
    """The tier this unit's own signals justify. Availability is a separate question.

    Deliberately no longer consults the capability mapping. A mapping answers "which model would I
    start for this tier", which is a question only asked when an Actor has to be created. Asking it
    first made an unmapped tier refuse work an already-staffed Actor could do, and made the
    no-configuration case refuse work the current session could simply have done itself.
    """
    return required_capability(unit)


def _select_actor(tier: str, unit: Unit, actors: list[Actor], preference: tuple[str, ...],
                  limits: dict[str, int]) -> tuple[Actor | None, str | None]:
    """Reuse within the chosen tier; a stronger Actor only as a named, recorded exception."""
    def ordered(candidates: list[Actor]) -> list[Actor]:
        # Deterministic, so two identical situations never staff differently. Operator preference
        # first, then a stable identifier, never "whichever the dict yielded".
        return sorted(candidates, key=lambda a: (
            preference.index(a.actor_id) if a.actor_id in preference else len(preference),
            a.actor_id,
        ))

    eligible = [a for a in actors if a.idle and (a.may_write or not unit.mutates)]
    exact = ordered([a for a in eligible if a.capability == tier])
    if exact:
        return exact[0], None

    # Nothing at the right tier. Creating one is the correct answer, and the caller does that. The
    # only case for reusing a stronger Actor is when the handoff overhead of a new one would exceed
    # the work itself, and that is a bounded exception which gets recorded as an exception rather
    # than quietly described as de-escalation.
    below_floor = unit.files_touched < limits["delegation_floor_files"] and unit.fully_specified
    stronger = ordered([a for a in eligible if rank(a.capability) > rank(tier)])
    if below_floor and stronger:
        return stronger[0], (
            f"reused {stronger[0].actor_id} at {stronger[0].capability} for a unit needing {tier}: "
            "below the delegation floor, so starting a cheaper Actor would cost more than the work"
        )
    return None, None


def route(unit: Unit, actors: list[Actor], available_capabilities: dict[str, Any],
          *, lease_available: bool = True, preference: tuple[str, ...] = (),
          limits: dict[str, int] | None = None) -> Decision:
    """Decide who does this unit. Authority, then eligibility, then economics, in that order."""
    limits = {**DEFAULT_LIMITS, **(limits or {})}

    # 1. Authority. Ahead of everything, including determinism: a scripted edit to a credential file
    #    is still a credential decision, and the cheapness of the method changes nothing about that.
    sensitive = [c for c in unit.sensitive_classes if c in SENSITIVE_CLASSES]
    unknown = [c for c in unit.sensitive_classes if c not in SENSITIVE_CLASSES]
    if unknown:
        raise RoutingError(f"unknown sensitive classes {unknown}; classify before routing")
    if sensitive:
        return Decision(ROUTE_HUMAN, None, None,
                        f"authority boundary: {', '.join(sensitive)}")

    # 2. Budgets. A cap sends work to the human, never one tier higher.
    exhausted = caps_exhausted(unit, limits)
    if exhausted:
        return Decision(ROUTE_HUMAN, None, None, f"{exhausted}; caps route to the human, not upward")

    # 3. Mutation-ownership eligibility, applied to every route rather than only to model launches.
    #    A deterministic formatter writing to the worktree is a writer like any other.
    if unit.mutates and not lease_available:
        return Decision(ROUTE_HUMAN, None, None,
                        "the worktree's write lease is unavailable; no route may mutate without it")

    # 4. Economics, at last, among what is permitted.
    if unit.deterministic_tool:
        return Decision(ROUTE_TOOL, TOOL, None,
                        f"a deterministic tool completes this: {unit.deterministic_tool}")

    tier = _cheapest_eligible_tier(unit)

    parent = next((a for a in actors if a.is_parent), None)
    below_floor = unit.files_touched < limits["delegation_floor_files"] and unit.fully_specified
    if (below_floor and parent is not None and rank(parent.capability) >= rank(tier)
            and (parent.may_write or not unit.mutates)):
        return Decision(ROUTE_ACTOR, tier, parent.actor_id,
                        "below the delegation floor: handoff would cost more than the work")

    chosen, exception = _select_actor(tier, unit, actors, preference, limits)
    if chosen is not None:
        return Decision(ROUTE_ACTOR, tier, chosen.actor_id,
                        f"reused the idle {tier} Actor", exception)

    # Nothing suitable is staffed, so one has to be created, and only now does the mapping matter.
    if tier not in available_capabilities:
        # A sufficient parent is the answer the no-configuration case has always had: the session
        # already running is an Actor, and doing the work itself needs no model to be started.
        if parent is not None and rank(parent.capability) >= rank(tier) \
                and (parent.may_write or not unit.mutates):
            return Decision(ROUTE_ACTOR, tier, parent.actor_id,
                            f"no {tier} Actor is staffed and none is mapped; the current session "
                            "is sufficient and starting one would cost more than doing it")
        raise RoutingError(
            f"unit {unit.unit_id} needs a {tier} Actor created, and this operator's staffing "
            "configuration maps no model to that capability; add the mapping or route the work to "
            "the human"
        )
    return Decision(ROUTE_ACTOR, tier, None,
                    f"no idle {tier} Actor is staffed; create one at {tier}")


def escalate(unit: Unit, limits: dict[str, int] | None = None) -> Unit:
    """Raise a unit one tier, on named evidence only, and never past its caps.

    Escalation asks a narrow question of a stronger Actor. It does not transfer ownership of the
    unit, and the answer does not make an incapable owner capable: the caller re-evaluates the
    remaining work afterwards rather than assuming the expensive Actor now holds it.
    """
    limits = {**DEFAULT_LIMITS, **(limits or {})}
    evidence = escalation_evidence(unit)
    if not evidence:
        raise RoutingError(
            f"unit {unit.unit_id} has no named evidence for escalation; a hunch is not a signal"
        )
    refused = escalation_refused(unit, limits)
    if refused:
        raise RoutingError(
            f"unit {unit.unit_id} is out of escalation budget ({refused}); this goes to the human "
            "rather than to a more expensive Actor"
        )
    return unit._replace(escalation_level=unit.escalation_level + 1,
                         consultations=unit.consultations + 1)
