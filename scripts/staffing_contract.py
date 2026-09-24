#!/usr/bin/env python3
"""The invocation contract for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Every invocation, created or resumed, states the same set of facts about what it may touch, and
    only the run's own coordinator may cause another invocation to exist.

Two ideas carry it, and both were learned the hard way in review.

1. **Create and resume must establish the same contract, or resume is not used.** A vendor that
   cannot restate a restriction when continuing a conversation has not kept it; it has merely
   stopped mentioning it. Where that cannot be demonstrated, the runtime creates a fresh session
   with the restriction applied rather than assuming an old one survived. A session that could
   write is never reused as an advisory reader, whatever its help text says.

2. **A flag represents a policy; only the runtime enforces one.** Orchestration authority is not
   inherited, not readable from the ambient environment, and not something an invocation can grant
   itself: a child context is *constructed* with it switched off, and the authoritative run state
   says who the coordinator is. A missing or contradictory context denies orchestration; it never
   becomes permission to start a fresh run instead.

The boundary is cooperative and stated as such. These values live in the environment of a process
the user already controls, so they order agent-base's own executions; they are not a security
barrier against arbitrary hostile code running as that user. Where a stronger guarantee is claimed,
it comes from an actual sandbox or tool restriction, named at the point where it is claimed.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, NamedTuple

ENVIRONMENT_KEY = "AB_STAFFING_CONTEXT"
CONTRACT_VERSION = 1

# What an invocation may do to the worktree. There is no "maybe": either the invocation holds the
# lease and may write, or it may not, and the launch arguments follow from that one fact.
WRITE_NONE = "none"
WRITE_WORKTREE = "worktree"
WRITE_SCOPES = (WRITE_NONE, WRITE_WORKTREE)

# Coarse tool classes, deliberately not a vendor's own tool names: the contract has to mean the same
# thing for a vendor that calls its file writer something else.
TOOL_READ = "read"
TOOL_WRITE = "write"
TOOL_EXECUTE = "execute"
TOOL_CLASSES = (TOOL_READ, TOOL_WRITE, TOOL_EXECUTE)

OUTPUT_ENVELOPE = "ab-result-envelope-v1"


class ContractError(RuntimeError):
    """Every refusal here. All of them fail closed."""


class Contract(NamedTuple):
    """One invocation's complete authority. Nothing about it is implied or inherited."""

    version: int
    run_id: str
    generation: int
    actor_id: str
    invocation_id: str
    worktree: str
    content_revision: str
    vendor: str
    model: str
    effort: str | None
    write_scope: str
    lease_token: str | None
    permitted_tool_classes: tuple[str, ...]
    provider_scope: str
    approval_mode: str
    output_protocol: str
    max_turns: int | None
    wall_clock_seconds: int
    orchestration: bool

    def as_dict(self) -> dict[str, Any]:
        value = self._asdict()
        value["permitted_tool_classes"] = list(self.permitted_tool_classes)
        return value

    def narrower_or_equal_to(self, other: "Contract") -> bool:
        """True when nothing this contract permits exceeds what `other` permits."""
        if self.write_scope == WRITE_WORKTREE and other.write_scope == WRITE_NONE:
            return False
        return set(self.permitted_tool_classes) <= set(other.permitted_tool_classes)


def build(*, run_id: str, generation: int, actor_id: str, invocation_id: str, worktree: Path,
          content_revision: str, vendor: str, model: str, effort: str | None, write_scope: str,
          lease_token: str | None, permitted_tool_classes: tuple[str, ...], provider_scope: Path,
          approval_mode: str, max_turns: int | None, wall_clock_seconds: int,
          orchestration: bool = False) -> Contract:
    """Assemble a contract, refusing any combination that cannot be honestly enforced.

    `orchestration` defaults to False here and everywhere else it appears. Authority to cause more
    invocations is the kind of thing that must be asked for explicitly, once, by the one component
    entitled to grant it.
    """
    if write_scope not in WRITE_SCOPES:
        raise ContractError(f"write scope must be one of {WRITE_SCOPES}, not {write_scope!r}")
    unknown = set(permitted_tool_classes) - set(TOOL_CLASSES)
    if unknown:
        raise ContractError(f"unknown tool classes: {sorted(unknown)}")
    if write_scope == WRITE_WORKTREE:
        if not lease_token:
            raise ContractError(
                "an invocation that may write must carry the worktree's lease token; write "
                "permission without the lease is exactly the unsynchronized writer the lease exists "
                "to prevent"
            )
        if TOOL_WRITE not in permitted_tool_classes:
            raise ContractError("a writing scope needs the write tool class")
    else:
        if lease_token:
            raise ContractError("a read-only invocation must not carry a lease token")
        if TOOL_WRITE in permitted_tool_classes:
            raise ContractError(
                "a read-only invocation must not be given the write tool class; an advisory Actor "
                "that can write is not advisory"
            )
    if wall_clock_seconds <= 0:
        raise ContractError("every invocation needs a positive wall-clock bound")
    if Path(provider_scope) != Path(worktree):
        raise ContractError(
            "the approved provider scope must name this exact worktree; a broader prefix would "
            "authorize data the human never approved"
        )
    return Contract(
        version=CONTRACT_VERSION, run_id=run_id, generation=generation, actor_id=actor_id,
        invocation_id=invocation_id, worktree=str(worktree), content_revision=content_revision,
        vendor=vendor, model=model, effort=effort, write_scope=write_scope,
        lease_token=lease_token, permitted_tool_classes=tuple(permitted_tool_classes),
        provider_scope=str(provider_scope), approval_mode=approval_mode,
        output_protocol=OUTPUT_ENVELOPE, max_turns=max_turns,
        wall_clock_seconds=wall_clock_seconds, orchestration=orchestration,
    )


def resumability(existing: Contract, desired: Contract, vendor_can_restate: bool) -> tuple[bool, str]:
    """Whether `desired` may continue `existing`'s conversation, and why not when it may not.

    The default answer is no. Saying yes requires the vendor to be able to restate the restriction
    on resume, which is a per-vendor fact somebody has to verify rather than infer from help text.
    """
    if existing.worktree != desired.worktree:
        return False, "a resumed session may not be pointed at a different worktree"
    if (existing.vendor, existing.model, existing.effort) != (
            desired.vendor, desired.model, desired.effort):
        return False, "model or effort changed; that is a different Actor, not a continuation"
    if existing.write_scope == WRITE_WORKTREE and desired.write_scope == WRITE_NONE:
        return False, (
            "a session that could write is never reused as an advisory reader: nothing proves the "
            "restriction now applies to a conversation that began without it"
        )
    if not desired.narrower_or_equal_to(existing):
        return False, "the requested authority exceeds what the existing session was created with"
    if not vendor_can_restate:
        return False, (
            "this vendor's resume path has not been shown to restate the contract, so a fresh "
            "restricted session is created instead"
        )
    return True, ""


def child_context(parent: Contract, *, actor_id: str, invocation_id: str, write_scope: str,
                  lease_token: str | None, permitted_tool_classes: tuple[str, ...],
                  model: str | None = None, effort: str | None = None, effort_given: bool = False,
                  content_revision: str | None = None,
                  max_turns: int | None = None,
                  wall_clock_seconds: int | None = None) -> Contract:
    """Construct the context a dispatched invocation runs under. Never copies authority upward.

    `orchestration` is absent from the signature on purpose. A child cannot be given it, cannot ask
    for it, and cannot inherit it, because the ordinary accidental failure is not a malicious child:
    it is an enabled value sitting in the coordinator's environment that a child picks up and uses
    to call itself a coordinator in a run of its own.

    Overriding the model requires stating the effort too, via `effort_given`, including when the
    intended effort is None. Inheriting a parent's effort under a different model silently pairs a
    setting with a model it was never chosen for, and `None` is a real value here rather than an
    absence, so a bare default cannot tell the two apart.
    """
    if model is not None and not effort_given:
        raise ContractError(
            "overriding a child's model requires stating its effort explicitly; an inherited "
            "effort belongs to the model it was chosen for"
        )
    return build(
        run_id=parent.run_id, generation=parent.generation, actor_id=actor_id,
        invocation_id=invocation_id, worktree=Path(parent.worktree),
        # The coordinator's revision is the state of the worktree when the run began, which stops
        # being this unit's baseline the moment any earlier unit writes. Inheriting it made a later
        # reader inspect current content while its own invocation still named the initial content.
        content_revision=content_revision or parent.content_revision, vendor=parent.vendor,
        model=model or parent.model, effort=effort if effort_given else parent.effort,
        write_scope=write_scope, lease_token=lease_token,
        permitted_tool_classes=permitted_tool_classes,
        provider_scope=Path(parent.provider_scope), approval_mode=parent.approval_mode,
        max_turns=max_turns if max_turns is not None else parent.max_turns,
        wall_clock_seconds=wall_clock_seconds or parent.wall_clock_seconds,
        orchestration=False,
    )


def to_environment(contract: Contract) -> dict[str, str]:
    """The environment overlay a launcher applies. Replaces any inherited value; never merges."""
    return {ENVIRONMENT_KEY: json.dumps(contract.as_dict(), sort_keys=True)}


def from_environment(environment: dict[str, str] | None = None) -> Contract | None:
    """The contract this process is running under, or None when it is not a dispatched invocation."""
    raw = (environment if environment is not None else os.environ).get(ENVIRONMENT_KEY)
    if not raw:
        return None
    try:
        value = json.loads(raw)
        value["permitted_tool_classes"] = tuple(value["permitted_tool_classes"])
        return Contract(**value)
    except (ValueError, TypeError, KeyError) as error:
        # A context that cannot be read is not an absent context. Treating it as absent is precisely
        # how a damaged child would promote itself to an unconstrained root.
        raise ContractError(
            f"{ENVIRONMENT_KEY} is present but unreadable ({error}); refusing to treat a damaged "
            "invocation context as no context at all"
        ) from error


def require_orchestration(contract: Contract, run_state: dict) -> None:
    """Permit causing further invocations, or refuse with the reason.

    Checked against the authoritative run document rather than against the contract alone, so a
    forged or stale claim of coordinator status does not survive contact with the run's own record.
    """
    if not contract.orchestration:
        raise ContractError(
            "this invocation may plan and propose a decomposition but not launch Actors; return "
            "the proposal to the coordinator"
        )
    if contract.run_id != run_state.get("run_id"):
        raise ContractError("this contract names a different run than the state it was checked against")
    if contract.generation != run_state.get("owner", {}).get("generation"):
        raise ContractError(
            "the run's ownership has moved since this contract was issued; a superseded "
            "coordinator must stop dispatching"
        )
    if contract.actor_id != run_state.get("coordinator_actor_id"):
        raise ContractError(
            f"actor {contract.actor_id} is not this run's coordinator "
            f"({run_state.get('coordinator_actor_id')})"
        )


def require_fresh_run_authority(environment: dict[str, str] | None = None) -> None:
    """Refuse to start a new run from inside a dispatched invocation.

    Without this, every other check is bypassable by the simplest route available: a child that may
    not orchestrate its own run simply starts a second run and orchestrates that one. A missing
    context is the ordinary human-launched case and is allowed; a present one never is.
    """
    context = from_environment(environment)
    if context is not None:
        raise ContractError(
            f"invocation {context.invocation_id} is running inside run {context.run_id} and may "
            "not bootstrap a new run; return a decomposition request to its coordinator instead"
        )


def require_launch_authority(contract: Contract | None, run_state: dict) -> None:
    """The single gate every launch path calls, whatever route it came from."""
    if contract is None:
        raise ContractError(
            "no invocation context: a launch must be dispatched by a coordinator that holds one"
        )
    require_orchestration(contract, run_state)
