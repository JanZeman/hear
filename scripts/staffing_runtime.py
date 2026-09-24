#!/usr/bin/env python3
"""Sequencing one work unit through the staffing components (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Nothing is launched that has not first been authorized, made eligible, recorded, and bounded;
    and nothing is closed that has not been independently confirmed.

The shape is two phases rather than one call with a callback, and that is a design choice worth
stating. `plan` decides and records; the caller executes; `settle` confirms and closes. Splitting
them means every decision in this file is testable without spending a model, and it makes the
central recovery property structural instead of careful: an invocation that is planned and never
settled stays `running`, which is exactly the record a crash should leave behind. There is no code
path that could forget to write it, because writing it is what `plan` does.

`settle` is deliberately unforgiving. A non-zero exit, a missing envelope, one addressed to another
invocation, or a claim of success over content the worktree has since moved past all resolve to
`uncertain` rather than to success or failure. Uncertain is the honest answer when nobody observed
the effects, and it is the answer that stops the next attempt until a human looks.
"""

from __future__ import annotations

import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import uuid
from typing import Any, Callable, NamedTuple


def _sibling(name: str):
    existing = sys.modules.get(name)
    if existing is not None:
        # Return the module already loaded, never a second copy of it. `setdefault` keeps the first
        # in `sys.modules` while the caller walks away with the second, so `except SomeError` stops
        # matching the class the other copy raises. Found when a test's `assertRaises` let a
        # correctly raised error through.
        return existing
    path = Path(__file__).resolve().with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


_lease = _sibling("staffing_lease")
_run = _sibling("staffing_run")
_contract = _sibling("staffing_contract")
_routing = _sibling("staffing_routing")
_config = _sibling("staffing_config")
_adapter = _sibling("staffing_adapter")
_result = _sibling("staffing_result")
_state = _sibling("staffing_state")


class RuntimeError_(RuntimeError):
    """Every refusal here. All of them fail closed."""


class Session(NamedTuple):
    run_id: str
    token: str
    coordinator: Any
    worktree: Path
    vendor: str
    config: Any


class Plan(NamedTuple):
    decision: Any
    invocation_id: str | None
    contract: Any | None
    launch: Any | None
    lease_token: str | None
    # How this unit is to be performed. Three routes reach this far and they are not the same act:
    # a deterministic tool the caller runs, the session already running doing the work itself, and
    # a separate vendor session the runtime starts. Recording which one was chosen is what lets a
    # test assert that a below-floor decision spent no child invocation.
    execution: str = "launch"

    @property
    def launches(self) -> bool:
        return self.launch is not None

    @property
    def performed_by_caller(self) -> bool:
        """True when the work is the caller's own hands: a tool it runs, or the session itself."""
        return self.execution in (EXECUTION_TOOL, EXECUTION_PARENT)


EXECUTION_TOOL = "tool"
EXECUTION_PARENT = "parent"
EXECUTION_LAUNCH = "launch"
EXECUTION_NONE = "none"


class Outcome(NamedTuple):
    state: str
    detail: str


class Verification(NamedTuple):
    """An observation made by something other than the Actor whose work it judges.

    Deliberately not a field on the result envelope. An Actor reporting that its own tests passed is
    the same class of evidence as an Actor reporting that it finished, and this exists precisely to
    be a different class.
    """

    passed: bool
    evidence: str


# What was established about writing having stopped, when the lease is handed back. Never defaulted
# into place: a process that crashed or timed out establishes nothing, and supplying the cooperative
# level on its behalf would report a hope as an observation.
QUIESCENCE_UNESTABLISHED = "unestablished"

# How strong a completion claim each level is. Used to cap a claim at what its route can actually
# establish, rather than to rank the levels for their own sake.
_COMPLETION_RANK = {
    QUIESCENCE_UNESTABLISHED: 0,
    _lease.COMPLETION_COOPERATIVE: 1,
    _lease.COMPLETION_OBSERVED: 2,
}


class Performed(NamedTuple):
    """What a `perform` callback reports back, including what it established about writing.

    `completion` exists because the previous interface had nowhere to put it, and the runtime filled
    the gap by inferring the cooperative level from a zero exit code. A callback that starts a
    writer in the background and returns zero satisfies that inference completely, and the lease was
    then handed back while the writer was still running. Returning normally is not a promise that
    nothing is still writing; it is only a promise that *this call* returned.

    A callback may still return a bare `(exit_code, evidence)` pair. That is read as establishing
    nothing, which keeps the worktree closed until somebody looks, and is the correct reading: an
    interface that cannot express the promise has not made it.
    """

    exit_code: int
    evidence: str
    completion: str = QUIESCENCE_UNESTABLISHED


def _new_id() -> str:
    return str(uuid.uuid4())


def write_probe(worktree: Path) -> str | None:
    """Establish that *this process* can write to the worktree, without launching anything.

    Returns None when writable, or the reason it is not. The guarantee is narrow and worth stating
    as such: it runs as the coordinator, so it proves the coordinator's filesystem access and says
    nothing about the Actor's sandbox, approval policy, or tools. Those are separate facts,
    established by the invocation contract and by the Actor's own behaviour.

    It is still worth doing, because the failure it does catch is cheap here and expensive later.
    Observed live on 2026-09-18: a writer returned successfully having written nothing, because its
    permission prompt reached nobody, and the cost of finding out that way was a model invocation
    and a worktree left uncertain.

    It mutates the worktree, briefly, so it runs inside ownership. Running it before acquisition
    left a window in which another coordinator held the lease while this one wrote.
    """
    marker = worktree / f".ab-staffing-write-probe-{uuid.uuid4().hex[:8]}"
    baseline = subprocess.run(["git", "-C", str(worktree), "status", "--porcelain=v1", "-z"],
                              capture_output=True, text=True, check=False).stdout
    try:
        marker.write_text("probe\n", encoding="utf-8")
        marker.unlink()
    except OSError as error:
        return f"the worktree is not writable from this process: {error}"
    if marker.exists():
        return "the write probe left a file behind; inspect it before staffing a writer here"
    after = subprocess.run(["git", "-C", str(worktree), "status", "--porcelain=v1", "-z"],
                           capture_output=True, text=True, check=False).stdout
    if after != baseline:
        return "the write probe changed the worktree; something else is writing here"
    return None


def plan(session: Session, unit: Any, actors: list[Any],
         *, actor_factory: Callable[[str], Any] | None = None) -> Plan:
    """Decide, authorize, record, and prepare. Launches nothing."""
    state = _run.read(session.run_id)
    # Only this run's coordinator may cause another invocation to exist, checked against the run's
    # own record rather than against the contract's claim about itself.
    _contract.require_launch_authority(session.coordinator, state)

    # The caller's counters are a hint; the record is the fact. A fresh Unit after each resolved
    # failure used to reset every budget, so a cap meant to stop repeated spending stopped nothing.
    unit = unit._replace(**_run.unit_counters(state, unit.unit_id))

    limits = {**_routing.DEFAULT_LIMITS, **(session.config.limits or {})}
    lease_state = _lease.status(session.worktree)["state"]
    decision = _routing.route(
        unit, actors,
        _config.available_capabilities(session.config, session.vendor),
        lease_available=(lease_state == "free"),
        preference=session.config.preference,
        limits=session.config.limits or None,
    )

    if decision.route == _routing.ROUTE_HUMAN:
        return Plan(decision, None, None, None, None, EXECUTION_NONE)

    invocation_id = _new_id()
    lease_token = None

    # Everything from here to the returned Plan is preparation. Nothing has been launched yet, so a
    # failure in it is knowable: the lease goes back cleanly and any invocation already recorded is
    # closed as failed, rather than left standing as work whose effects nobody observed. An
    # unrecoverable-looking stranded lease over work that never started is a worse outcome than the
    # error that caused it.
    #
    # The write probe belongs inside this boundary and used to sit above it. It is the one piece of
    # preparation that writes, so it is also the likeliest to raise, and a raised probe left the
    # lease it had just taken standing over a worktree nothing had touched.
    recorded = False
    try:
        if unit.mutates:
            # Acquired before anything touches the worktree, including this runtime's own probe, so
            # no window exists in which a second writer could be authorized.
            lease_token = _lease.acquire(
                session.worktree, session.run_id, decision.actor_id or "new", invocation_id
            ).token
            unwritable = write_probe(session.worktree)
            if unwritable:
                # Nothing ran, so the lease is given back cleanly rather than left standing. A
                # writer that cannot write does not fail loudly; it returns a polite explanation and
                # an unchanged tree, and finding that out costs a model invocation if left until
                # then.
                _lease.release(session.worktree, lease_token, _lease.COMPLETION_OBSERVED,
                               "preparation only; nothing was launched")
                return Plan(
                    _routing.Decision(_routing.ROUTE_HUMAN, None, None, unwritable), None, None,
                    None, None, EXECUTION_NONE,
                )

        # Observed here, once, and after ownership where there is any: this unit's baseline is what
        # the worktree holds now, not what it held when the run started.
        baseline = _run.content_revision(session.worktree)

        if decision.route == _routing.ROUTE_TOOL:
            # A deterministic tool that writes is a writer like any other: it holds the lease, and its
            # run is recorded, because "it was only a formatter" is not a reason for an unobserved
            # mutation to go unrecorded.
            _run.begin_invocation(
                session.run_id, session.token, invocation_id=invocation_id, unit_id=unit.unit_id,
                actor_id=_tool_actor(session, decision), constraints_revision=decision.reason,
                content_revision_value=baseline,
                attempt_cap=limits["max_attempts_per_unit"], label=getattr(unit, "label", None),
            )
            recorded = True
            return Plan(decision, invocation_id, None, None, lease_token, EXECUTION_TOOL)

        actor_id = decision.actor_id
        if actor_id is None:
            if actor_factory is None:
                raise RuntimeError_(
                    f"no idle {decision.capability} Actor and no way to create one; supply an "
                    "actor_factory or staff the tier before dispatching"
                )
            actor_id = actor_factory(decision.capability)

        # The session already running is an Actor. When routing names it, the work is done here: no
        # model is started, no contract is constructed, no command is built. Before this branch existed,
        # a decision meaning "doing this yourself is cheaper than handing it over" went on to start a
        # model anyway, which inverted the economics it had just reasoned about.
        if any(a.actor_id == actor_id and a.is_parent for a in actors):
            if actor_id not in state["actors"]:
                _run.register_actor(session.run_id, session.token, actor_id, session.vendor,
                                    session.coordinator.model, session.coordinator.effort,
                                    decision.capability)
            _run.begin_invocation(
                session.run_id, session.token, invocation_id=invocation_id, unit_id=unit.unit_id,
                actor_id=actor_id, constraints_revision=decision.reason,
                content_revision_value=baseline,
                attempt_cap=limits["max_attempts_per_unit"], label=getattr(unit, "label", None),
            )
            recorded = True
            return Plan(decision, invocation_id, None, None, lease_token, EXECUTION_PARENT)

        # The model comes from the Actor that was selected, never from the tier the unit needed.
        # Those are the same number only when routing creates an Actor for the unit. When it reuses
        # a stronger idle one, `decision.capability` is what the work requires and the Actor is one
        # rung up; looking the mapping up by capability launched the cheap model while the ledger
        # recorded the strong Actor as having done the work, and refused outright when the cheap
        # tier was unmapped even though a sufficient Actor was standing there.
        #
        # Read from the run record rather than from the `actors` argument: `actor_factory` may have
        # registered this Actor a moment ago, after the read at the top of this function.
        registered = _run.read(session.run_id)["actors"].get(actor_id)
        if registered is None:
            raise RuntimeError_(
                f"actor {actor_id} is not registered in run {session.run_id}; an Actor's model is "
                "read from its registration, so whoever creates one registers it before it is "
                "dispatched"
            )
        if registered["vendor"] != session.vendor:
            # An invocation contract inherits its vendor from the coordinator, so there is no way to
            # express this one. Refusing is the honest answer; launching it under this session's
            # vendor would run a model nobody selected.
            raise RuntimeError_(
                f"actor {actor_id} is registered with vendor {registered['vendor']!r} and this "
                f"session is {session.vendor!r}; an invocation cannot cross vendors"
            )
        placement = _config.Placement(
            registered["vendor"], registered["model"], registered["effort"])
        invocation_contract = _contract.child_context(
            session.coordinator, actor_id=actor_id, invocation_id=invocation_id,
            write_scope=_contract.WRITE_WORKTREE if unit.mutates else _contract.WRITE_NONE,
            lease_token=lease_token,
            permitted_tool_classes=(_contract.TOOL_READ, _contract.TOOL_WRITE) if unit.mutates
            else (_contract.TOOL_READ,),
            model=placement.model, effort=placement.effort, effort_given=True,
            content_revision=baseline,
        )
        _run.begin_invocation(
            session.run_id, session.token, invocation_id=invocation_id, unit_id=unit.unit_id,
            actor_id=actor_id, constraints_revision=invocation_contract.content_revision,
            content_revision_value=invocation_contract.content_revision,
            attempt_cap=limits["max_attempts_per_unit"], label=getattr(unit, "label", None),
        )
        recorded = True
        launch = _adapter.build_launch(
            invocation_contract,
            resume_verified=_config.resume_is_verified(session.config, session.vendor),
        )
        return Plan(decision, invocation_id, invocation_contract, launch, lease_token,
                    EXECUTION_LAUNCH)
    except BaseException as error:
        if recorded:
            _run.complete_invocation(
                session.run_id, session.token, invocation_id, _run.FAILED,
                f"preparation failed before anything was launched: {error}")
        if lease_token:
            _lease.release(session.worktree, lease_token, _lease.COMPLETION_OBSERVED,
                           "preparation only; nothing was launched")
        if isinstance(error, _run.BudgetExhausted):
            # Not a failure of the run: the answer routing would have given had it read the counter
            # a moment later. A reached cap goes to the human everywhere else, and it does here.
            return Plan(_routing.Decision(_routing.ROUTE_HUMAN, None, None, str(error)),
                        None, None, None, None, EXECUTION_NONE)
        raise


def _tool_actor(session: Session, decision: Any) -> str:
    """Deterministic tools run under a named pseudo-Actor so the ledger is complete."""
    state = _run.read(session.run_id)
    if "deterministic-tool" not in state["actors"]:
        _run.register_actor(session.run_id, session.token, "deterministic-tool",
                            vendor="none", model="none", effort=None, capability=_routing.TOOL)
    return "deterministic-tool"


def settle(session: Session, prepared: Plan, *, exit_code: int, stdout: str,
           completion: str | None = None, evidence: str = "", completion_note: str = "",
           verification: Verification | None = None) -> Outcome:
    """Confirm what happened and close the records. Believes nothing it has not checked.

    Three observations, kept apart because they answer different questions and can disagree. The
    *claim* is what the Actor said about its own work. The *verification* is what something else
    established about that work. The *quiescence* is whether writing stopped, which is about the
    worktree rather than about the task: a correct result and a writer still running are both
    possible at once, and only the second is a reason to close the tree.

    An unverified claim is reported as `unverified` rather than promoted to success. That is the
    whole of this function's change from its first version, which accepted an envelope carrying
    nothing but its own addressing and returned `succeeded`.
    """
    if prepared.invocation_id is None:
        return Outcome("human", prepared.decision.reason)

    unit_id = _run.read(session.run_id)["invocations"][prepared.invocation_id]["unit_id"]
    observed = _run.content_revision(session.worktree)

    _judge.addressed = None
    outcome, detail = _judge(prepared, exit_code, stdout, unit_id, observed, verification)
    if _judge.addressed is not None:
        # Recorded beside the outcome, because a weakly addressed result is accepted on the
        # strength of this transport and a later reader cannot tell which ones those were.
        _run.record_addressing(session.run_id, session.token, prepared.invocation_id,
                               _judge.addressed)
    if evidence and not stdout:
        detail = f"{detail}: {evidence}".strip(": ") if detail else evidence

    # The run document keeps its own staleness net, and it is a reader's net: "the tree moved under
    # the result". Handing it a writer's post-edit revision makes every successful write look stale
    # there, so the record contradicted the outcome this function had just returned. Judged once,
    # above, by role; only a reader's revision is offered for the second check.
    reader_revision = (
        observed if outcome == _run.SUCCEEDED and prepared.contract is not None
        and prepared.contract.write_scope == _contract.WRITE_NONE else None
    )
    _run.complete_invocation(
        session.run_id, session.token, prepared.invocation_id, outcome, detail,
        result_content_revision=reader_revision,
    )
    if prepared.lease_token:
        # The lease's reason is about the *worktree*, so it carries what was said about writing
        # rather than what was concluded about the task. Whoever reclaims this reads only this.
        _lease.release(session.worktree, prepared.lease_token,
                       _quiescence(prepared, exit_code, outcome, completion),
                       completion_note or detail)
    # The caller is told what the record says, not what this function computed on the way there.
    # These disagreed once already: `settle` returned success while the ledger held uncertain, and
    # finished work stayed listed as unresolved.
    recorded = _run.read(session.run_id)["invocations"][prepared.invocation_id]["outcome"]
    return Outcome(recorded, detail)


def _quiescence(prepared: Plan, exit_code: int, outcome: str, completion: str | None) -> str:
    """What was established about writing having stopped, never what we would like to be true.

    Only what somebody actually reported gets through here. The previous version returned the
    cooperative level for any invocation that exited zero, which reads a normal return as a promise
    about what is still running, and those are different facts: a callback that starts an
    asynchronous writer and returns zero satisfies the first and violates the second. Nothing
    reported means nothing established, and the worktree stays closed until somebody looks.

    A claim is also capped at what its route can establish. A launch route can offer no more than
    the adapter declares in `Launch.completion_evidence`, because the promise belongs to the vendor
    CLI rather than to the runtime that started it. Claiming above the ceiling is not an error to
    raise; it is a claim to reduce, since the caller may be reporting honestly about something it
    was wrong about.
    """
    if completion is None:
        return QUIESCENCE_UNESTABLISHED
    if completion not in _COMPLETION_RANK:
        raise RuntimeError_(
            f"{completion!r} is not a completion level; the levels are "
            f"{sorted(_COMPLETION_RANK)}"
        )
    if exit_code != 0 or outcome == _run.UNCERTAIN:
        # Whatever was claimed, the invocation did not end the way the claim assumes.
        return QUIESCENCE_UNESTABLISHED
    ceiling = prepared.launch.completion_ceiling if prepared.launch is not None else None
    if ceiling in _COMPLETION_RANK and _COMPLETION_RANK[completion] > _COMPLETION_RANK[ceiling]:
        return ceiling
    return completion


def escalate(session: Session, unit: Any) -> Any:
    """Move one unit up a tier, counting the consultation where it cannot be forgotten.

    The routing rule decides whether the evidence justifies it; this records that it happened, so
    the next `plan` reads a budget that reflects what was actually spent.
    """
    # The same overlay `plan` does, for the same reason. Asking routing about the caller's counters
    # would let a forgetful caller escalate forever, which is exactly the budget this records.
    counted = unit._replace(**_run.unit_counters(_run.read(session.run_id), unit.unit_id))
    limits = {**_routing.DEFAULT_LIMITS, **(session.config.limits or {})}
    raised = _routing.escalate(counted, session.config.limits or None)
    try:
        # Re-checked where it is spent. The routing check above read these counters outside the
        # run's lock, so two escalations could both see the final available consultation.
        _run.record_escalation(session.run_id, session.token, unit.unit_id,
                               consultation_cap=limits["max_consultations_per_unit"],
                               level_cap=limits["max_escalation_levels"])
    except _run.BudgetExhausted as exhausted:
        raise _routing.RoutingError(str(exhausted)) from exhausted
    _receipt("escalation", session, actor="coordinator",
             capability=_routing.required_capability(raised))
    return raised


def _receipt(event: str, session: Session, **fields) -> None:
    """Record one content-free staffing receipt, and never let telemetry break a run.

    Loaded here rather than at import time because the receipt store is optional: a repository that
    has not adopted it should staff work normally and simply record nothing.
    """
    try:
        path = Path(__file__).resolve().with_name("route-receipts.py")
        spec = importlib.util.spec_from_file_location("route_receipts", path)
        module = sys.modules.get("route_receipts")
        if module is None:
            module = importlib.util.module_from_spec(spec)
            sys.modules["route_receipts"] = module
            spec.loader.exec_module(module)
        module.record_staffing(event, run=session.run_id, vendor=session.vendor, **fields)
    except Exception:
        # Instrumentation that can fail a run is worse than instrumentation that is missing.
        pass


def dispatch(session: Session, unit: Any, actors: list[Any], *,
             unit_goal: str, files: tuple[str, ...] = (),
             perform: Callable[[Plan], tuple[int, str]] | None = None,
             verify: Callable[[Plan], Verification] | None = None,
             actor_factory: Callable[[str], Any] | None = None) -> Outcome:
    """The supported way to put one work unit through the runtime, whichever route it takes.

    This exists because `plan` and `settle` alone left the executor contract to whoever called them,
    and every caller then invented its own: which prompt an Actor receives, how a vendor's output is
    translated, when the provider handle is captured, whether anything is recorded. Three callers
    would drift the way four launch builders drifted, and the first live runs used harness code that
    no test could reach.

    `perform` is the caller's own hands, and is required only for the two routes that are: a
    deterministic tool it runs, and the session itself doing the work. It returns an exit code and a
    line of evidence. The runtime keeps ownership, recording, and judgement for all three routes, so
    a tool that writes and a vendor session that writes are settled by the same protocol.

    `verify` is what makes a claim a result. Without it every route settles as `unverified`, which
    is the honest state and not a failure: the work may well be done, and nobody looked.
    """
    prepared = plan(session, unit, actors, actor_factory=actor_factory)

    if prepared.decision.route == _routing.ROUTE_HUMAN or prepared.invocation_id is None:
        # No receipt: there is no invocation to start. Emitting one here left an `invocation_start`
        # with no identifier and no matching stop, which the receipt reader reports as unmatched
        # for the rest of the run.
        return Outcome("human", prepared.decision.reason)

    # Both read from the record rather than from the caller. The Actor may have been created during
    # preparation, so the decision still calls it unassigned, and the attempt number is the ledger's
    # own count rather than the caller's, which a fresh Unit resets.
    started = _run.read(session.run_id)["invocations"][prepared.invocation_id]
    _receipt("invocation_start", session, actor=started["actor_id"],
             invocation=prepared.invocation_id, capability=prepared.decision.capability,
             attempt=started["attempt"])

    # Only a launched vendor reports what it spent. The caller's own hands cost tokens nobody here
    # can count, and a zero would be a lie that averages into every later estimate.
    spent = _adapter.Usage()

    if prepared.performed_by_caller and perform is None:
        # Known before anything could have started, so the lease and the invocation are given back
        # cleanly and the caller still gets its error. Raising straight out of here left both
        # standing over work that never began, which reads on recovery exactly like a crashed
        # writer and is the one failure shape that is nobody's to resolve.
        _abandon(session, prepared, f"no perform callback for the {prepared.execution} route")
        raise RuntimeError_(
            f"this unit routed to {prepared.execution}, which is the caller's own hands; "
            "supply perform= to dispatch it"
        )

    if prepared.performed_by_caller:
        try:
            reported = perform(prepared)
            if not isinstance(reported, Performed):
                # A bare pair is accepted and read as establishing nothing. See `Performed`.
                reported = Performed(*reported)
        except BaseException as error:
            # The callback may have written before it raised. Nothing here can distinguish a
            # callback that failed early from one that failed after mutating, so the honest record
            # is uncertainty, and the worktree stays closed. Never a clean release: that would be a
            # claim about effects nobody observed.
            outcome = settle(session, prepared, exit_code=70, stdout="",
                             evidence=f"the perform callback raised: {error}")
            _stop_receipt(session, prepared, outcome)
            raise
        outcome = settle(session, prepared, exit_code=reported.exit_code, stdout="",
                         evidence=reported.evidence, completion=reported.completion,
                         verification=_verification(session, prepared, verify))
    else:
        try:
            prompt = (
                _state.briefing(session.run_id, unit_goal=unit_goal, files=list(files))
                # The obligation is stated to the Actor before the envelope asks it to acknowledge
                # one. An acknowledgement of something nobody asked for is not a protocol, which is
                # what the cooperative level was until now.
                + "\n## Finishing\n\n" + _result.NO_BACKGROUND_WRITES
                + "\n\n## Required final output\n\nEmit exactly this JSON object on its own "
                + "line, filled in:\n\n"
                + _result.template(prepared.invocation_id, unit.unit_id)
                + f"\n\nUse content_revision exactly: {prepared.contract.content_revision}\n"
            )
            environment = dict(os.environ)
            environment.update(prepared.launch.environment)
        except BaseException as error:
            # Building a briefing or a prompt touches nothing outside this process, so a failure
            # here is knowable and releases cleanly.
            _abandon(session, prepared, f"the invocation was never started: {error}")
            raise

        try:
            completed = subprocess.run(
                prepared.launch.argv, input=prompt, cwd=prepared.launch.cwd, env=environment,
                capture_output=True, text=True, timeout=prepared.launch.timeout_seconds,
            )
        except subprocess.TimeoutExpired:
            # A timed-out writer may have written. Uncertain is the only honest outcome, and it is
            # the one that keeps the worktree closed until somebody looks.
            outcome = settle(session, prepared, exit_code=124, stdout="",
                             evidence="wall-clock bound reached; effects were not observed")
        except (FileNotFoundError, PermissionError) as error:
            # The command was never executed, which is a different fact from a command that ran and
            # failed. Separated deliberately: this one releases cleanly, and treating it as
            # uncertainty would close a worktree over a typo in an executable name.
            _abandon(session, prepared, f"the vendor command could not be started: {error}")
            raise
        except BaseException as error:
            outcome = settle(session, prepared, exit_code=70, stdout="",
                             evidence=f"the invocation ended abnormally: {error}")
            _stop_receipt(session, prepared, outcome)
            raise
        else:
            spent = _adapter.usage(session.vendor, completed.stdout)
            handle = _adapter.provider_handle(session.vendor, completed.stdout)
            if handle:
                # Only what the vendor actually returned. Recording the runtime's own invocation id
                # here made every run look recoverable, including the vendors that never gave one.
                _run.record_provider_handle(session.run_id, session.token,
                                            prepared.invocation_id, handle)
            stdout = _adapter.normalize_output(session.vendor, completed.stdout)
            outcome = settle(session, prepared, exit_code=completed.returncode, stdout=stdout,
                             completion=_acknowledged(prepared, stdout, unit.unit_id),
                             completion_note=_completion_note(prepared, stdout, unit.unit_id),
                             verification=_verification(session, prepared, verify))

    _stop_receipt(session, prepared, outcome, spent)
    return outcome


def _stop_receipt(session: Session, prepared: Plan, outcome: Outcome,
                  spent: Any = None) -> None:
    """One receipt per invocation, naming what was chosen and what it actually cost.

    The cost is the point of recording the choice. Effort multipliers are meant to come from
    ordinary work accumulating here rather than from sessions run to be measured, so an invocation
    that reports no counts contributes nothing rather than a zero.
    """
    if prepared.invocation_id is None:
        return
    state = _run.read(session.run_id)
    record = state["invocations"].get(prepared.invocation_id, {})
    actor = state["actors"].get(record.get("actor_id"), {})
    fields: dict[str, Any] = {
        "actor": record.get("actor_id", "unassigned"),
        "invocation": prepared.invocation_id,
        "capability": prepared.decision.capability,
        "outcome": outcome.state,
    }
    if actor.get("model"):
        fields["model"] = actor["model"]
    if actor.get("effort"):
        fields["effort"] = actor["effort"]
    spent = spent if spent is not None else _adapter.Usage()
    for name, value in (("total_input_tokens", spent.total_input),
                        ("uncached_input_tokens", spent.uncached_input),
                        ("cache_read_tokens", spent.cache_read),
                        ("cache_write_tokens", spent.cache_write),
                        ("output_tokens", spent.output),
                        ("thinking_tokens", spent.thinking),
                        ("usage_schema", spent.schema)):
        if value is not None:
            fields[name] = value
    _receipt("invocation_stop", session, **fields)


def _verification(session: Session, prepared: Plan,
                  verify: Callable[[Plan], Verification] | None) -> Verification | None:
    """Run the verification callback, and treat its own failure as an unchecked result.

    A verification that raises leaves the task unverified, which is what `settle` already reports
    for work nobody checked. It is deliberately not uncertainty: quiescence is about the worktree
    and was established by the route, while this is about the task. Conflating them would close the
    tree every time a checker had a bad day.
    """
    if verify is None:
        return None
    try:
        return verify(prepared)
    except BaseException:
        # Returned as "nobody checked", which is exactly what happened. Not recorded as a receipt:
        # the receipt vocabulary is deliberately closed, and this is not a staffing event.
        return None


def _abandon(session: Session, prepared: Plan, reason: str) -> None:
    """Give back a lease and close an invocation for work that provably never started.

    Only for failures before any effect was possible. Everything after that goes through `settle`,
    which records uncertainty rather than claiming the worktree is quiet.
    """
    if prepared.invocation_id is not None:
        _run.complete_invocation(session.run_id, session.token, prepared.invocation_id,
                                 _run.FAILED, reason)
    if prepared.lease_token:
        _lease.release(session.worktree, prepared.lease_token, _lease.COMPLETION_OBSERVED, reason)


def _acknowledged(prepared: Plan, stdout: str, unit_id: str) -> str | None:
    """What this invocation said about what it left running, or None when it said nothing.

    Read from the result envelope rather than assigned from the route. The Actor was given the
    no-background-writes obligation in its prompt and the envelope is where it answers; a CLI that
    exits zero has answered nothing, and crediting it with the cooperative level was the defect.

    An envelope that cannot be read at all yields None, which settles as unestablished and holds the
    worktree. That is the same answer an unparseable result already gets for the task itself, and
    for the same reason: silence is not agreement.
    """
    try:
        envelope = _result.parse(stdout, invocation_id=prepared.invocation_id, unit_id=unit_id)
    except _result.EnvelopeError:
        return None
    if envelope.completion == _result.COMPLETION_QUIET:
        # Capped at what the route could offer. A vendor CLI cannot give a mechanically observed
        # completion however sincerely its Actor answers, and `_quiescence` applies the ceiling.
        return _lease.COMPLETION_COOPERATIVE
    return None


def _completion_note(prepared: Plan, stdout: str, unit_id: str) -> str:
    """What the invocation said about what it left running, for whoever has to recover it.

    `left_running` and `unknown` both establish nothing and they are not the same message. One
    names a writer somebody can go and look for; the other says the question went unanswered. The
    uncertain record carries the difference, which is the only place it can be read later.
    """
    try:
        envelope = _result.parse(stdout, invocation_id=prepared.invocation_id, unit_id=unit_id)
    except _result.EnvelopeError:
        return "no readable result, so nothing was established about background writing"
    if envelope.completion == _result.COMPLETION_QUIET:
        return "the Actor acknowledged leaving nothing writing"
    if envelope.completion == _result.COMPLETION_LEFT_RUNNING:
        return ("the Actor reported leaving something writing: "
                f"{envelope.evidence or 'no detail given'}")
    if envelope.completion is not None:
        return (f"the Actor answered {envelope.completion!r}, which is not one of "
                f"{list(_result.COMPLETIONS)}; the obligation and the answer disagree")
    return "the Actor did not answer the no-background-writes obligation"


def _judge(prepared: Plan, exit_code: int, stdout: str, unit_id: str, observed: str,
           verification: Verification | None = None):
    if exit_code != 0:
        # A process that did not finish may still have written. Failure would claim knowledge of
        # that; uncertainty does not.
        return _run.UNCERTAIN, f"exit code {exit_code}; effects on the worktree were not observed"
    if prepared.performed_by_caller:
        # The caller performed it and is the only witness there is; its own verification decides.
        if verification is None:
            return _run.UNVERIFIED, prepared.decision.reason
        if not verification.passed:
            return _run.FAILED, verification.evidence
        return _run.SUCCEEDED, verification.evidence or prepared.decision.reason
    try:
        envelope = _result.parse(stdout, invocation_id=prepared.invocation_id, unit_id=unit_id)
    except _result.EnvelopeError as error:
        return _run.UNCERTAIN, str(error)
    _judge.addressed = envelope.addressed_by_invocation
    if not envelope.claims_success:
        return _run.FAILED, f"Actor claimed {envelope.claim}: {envelope.evidence}"
    stale = _staleness(prepared, envelope, observed)
    if stale:
        return _run.UNCERTAIN, stale
    if verification is None:
        return _run.UNVERIFIED, (
            f"the Actor claims completion and nothing checked it: {envelope.evidence}".rstrip(": ")
        )
    if not verification.passed:
        # The Actor said it finished and an independent check disagrees. That is a failure, and the
        # Actor's confidence is not evidence against the check.
        return _run.FAILED, f"verification failed: {verification.evidence}"
    return _run.SUCCEEDED, verification.evidence or envelope.evidence


def _staleness(prepared: Plan, envelope, observed: str) -> str | None:
    """Whether the Actor's stated content revision still means anything, which depends on its role.

    The two roles want opposite comparisons, and conflating them is a mistake that looks harmless in
    a unit test written for only one of them. A reader's result describes what it inspected, so it
    must match what is there now: if the tree moved underneath it, its verification speaks for
    content that no longer exists. A writer's result describes work it performed, so it must match
    the baseline it was given, and the tree is *expected* to differ afterwards. Comparing a writer's
    echoed baseline against the post-edit revision marks every successful write as uncertain, which
    is what a first live run found this doing.
    """
    if not envelope.content_revision:
        return None
    if prepared.contract is not None and \
            prepared.contract.write_scope == _contract.WRITE_WORKTREE:
        if envelope.content_revision != prepared.contract.content_revision:
            return (
                "the Actor worked from a different baseline than the one its invocation was given; "
                "what it changed cannot be attributed to this unit"
            )
        return None
    if envelope.content_revision != observed:
        return (
            "the Actor described content the worktree has since moved past; its verification "
            "cannot speak for what is there now"
        )
    return None
