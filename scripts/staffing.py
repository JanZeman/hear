#!/usr/bin/env python3
"""The way in. One command a coordinating session runs to staff a unit of work (item 160).

Everything under this file existed for two days before anything could use it. Nine modules were
distributed to every client and the only entry was forty lines of Python: a `Session`, a coordinator
contract with a dozen parameters, an actor factory, a `Unit`. That boilerplate lived in exactly two
places, both of them agent-base's own test harnesses, so a downstream agent could read the skill and
still not start. This is that boilerplate, written once.

    python3 scripts/staffing.py plan  --goal "..."                  # who would do it, and why
    python3 scripts/staffing.py run   --goal "..." --files a.py --mutates --verify "pytest -q"
    python3 scripts/staffing.py status                              # what is open, what is held
    python3 scripts/staffing.py recover --evidence "..."            # reopen a held worktree

`plan` answers without committing to anything, which the API could not: asking routing a question
used to require `run.create`, and creating a run takes ownership, persists, and refuses to happen
twice. A dry run left a record that blocked the real one.

The run is keyed to the worktree and the day, so units dispatched from the same session accumulate
in one ledger and a new day starts a new one. A coordinator that wants a different arrangement
passes `--run`.

What this deliberately does not do is decide anything. Which tier, whether the unit mutates, what
counts as verified: all of that stays with the caller, because a convenience that guesses those is
how a cost control becomes a cost.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shlex
import subprocess
import sys
from typing import Any


def _sibling(name: str):
    existing = sys.modules.get(name)
    if existing is not None:
        return existing
    path = Path(__file__).resolve().with_name(f"{name}.py")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


_runtime = _sibling("staffing_runtime")
_lease = _sibling("staffing_lease")
_run = _sibling("staffing_run")
_contract = _sibling("staffing_contract")
_routing = _sibling("staffing_routing")
_config = _sibling("staffing_config")


class StaffingError(RuntimeError):
    """Every refusal here, phrased for somebody who has not read the modules underneath."""


class YoursToFinish(StaffingError):
    """The caller is to do this, and the worktree is held for it until the caller says otherwise.

    Carries the Plan so `finish` can settle exactly this invocation. Nothing else may write to the
    worktree meanwhile, which is the point: the earlier version told the caller to proceed and took
    nothing, so a second caller was told the same thing about the same free worktree.
    """

    def __init__(self, reason: str, prepared: Any, session: Any):
        super().__init__(reason)
        self.prepared = prepared
        self.session = session


class ReservationLost(StaffingError):
    """The worktree was taken between deciding and preparing, so nothing is reserved.

    Distinct from `DoItYourself`, which it used to be raised as. They read alike and mean opposite
    things: one says the caller should do the work, this one says it must not, because somebody
    else holds the exclusion. Conflating them made the command exit zero with "route yourself" over
    a worktree the caller had no claim to.
    """


class DoItYourself(StaffingError):
    """Not a refusal: the economics say handing this over costs more than doing it.

    Raised rather than returned because the caller of `staff()` is about to wait for an Actor that
    should never be started, and a return value that says so is easier to ignore than an exception.
    """


def coordinator_identity() -> str:
    """Who is calling, as far as anything here can establish.

    A run's ledger is adopted across separate invocations of this command, and the earlier version
    adopted it on the strength of the worktree and the date, then read the ownership token off disk.
    A token read out of the thing being adopted is not evidence of owning it: any process that can
    run this command could take over a ledger, and two coordinating sessions could hold the same
    token without the explicit takeover the run API requires.

    `AB_COORDINATOR_ID` if the operator sets one, otherwise the vendor's own session identifier,
    otherwise `unidentified`. The last of those is honest rather than safe: it means adoption cannot
    be distinguished from takeover, so it is refused and the caller is sent to `take_ownership`.
    """
    for name in ("AB_COORDINATOR_ID", "CLAUDE_CODE_SESSION_ID", "CODEX_SESSION_ID"):
        found = os.environ.get(name)
        if found:
            return f"{name.split('_')[0].lower()}:{found}"
    return "unidentified"


def worktree_of(path: str | Path | None = None) -> Path:
    return _lease.canonical_worktree(Path(path or Path.cwd()))


def default_run_id(tree: Path) -> str:
    """One ledger per worktree per day. Long enough to accumulate, short enough to stay readable.

    The path is folded in, not only the directory name: run storage is shared across worktrees, so
    `/one/repo` and `/two/repo` produced the same identifier and the second was refused rather than
    given its own. The name stays in front because a person has to recognise it.
    """
    stamp = hashlib.sha256(str(tree).encode("utf-8")).hexdigest()[:6]
    return f"{tree.name}-{stamp}-{dt.date.today().isoformat()}"


def label_from(goal: str) -> str:
    """A few words a person can recognise a week later, taken from the goal itself.

    The first sentence, or the first line, whichever ends sooner. The caller can always pass
    something better; what this avoids is the default being a timestamp, which is what `status` used
    to show and which tells a reader nothing about what ran.
    """
    first = goal.strip().split("\n", 1)[0]
    stop = first.find(". ")
    if stop > 0:
        first = first[:stop]
    return first.strip()[:120]


def unit_from(goal: str, *, unit_id: str, files: tuple[str, ...], mutates: bool,
              mechanical: bool, fully_specified: bool, label: str | None = None,
              **signals: Any):
    return _routing.Unit(unit_id=unit_id, label=label or label_from(goal), mechanical=mechanical,
                         fully_specified=fully_specified, files_touched=len(files) or 1,
                         mutates=mutates, **signals)


def open_session(tree: Path, vendor: str, run_id: str) -> Any:
    """Adopt today's ledger for this worktree, or start one.

    Adoption rather than creation is the difference between a usable entry point and one that works
    once a day. `create` refuses an existing run on purpose, because silently adopting somebody
    else's is how two coordinators end up believing they own the same work; here the caller is the
    same session that created it, and the token is read back from the record it already owns.
    """
    # Before anything is adopted or executed. A dispatched child carries an invocation contract in
    # its environment, and without this check a child with a shell could reach this command and
    # coordinate a run of its own: the bootstrap guard was being enforced by `create` and stepped
    # around entirely by adoption.
    _contract.require_fresh_run_authority()

    identity = coordinator_identity()
    existing = None
    try:
        existing = _run.read(run_id)
    except _run.RunError:
        pass

    if existing is None:
        token = _run.create(run_id, identity, tree, coordinator_actor_id="coordinator")
    else:
        if Path(existing["worktree"]).resolve() != tree:
            raise StaffingError(
                f"run {run_id} belongs to {existing['worktree']}, not {tree}; pass --run to name a "
                "different ledger"
            )
        owner = existing["owner"]["coordinator_id"]
        if owner != identity or identity == "unidentified":
            raise StaffingError(
                f"run {run_id} is owned by {owner} and this session is {identity}; continuing "
                "somebody else's run is a takeover and has to be one. Use "
                "`staffing_run.take_ownership` with evidence, or pass --run to start your own "
                "ledger."
            )
        token = existing["owner"]["token"]

    if "coordinator" not in _run.read(run_id)["actors"]:
        _run.register_actor(run_id, token, "coordinator", vendor, "coordinator-session", None,
                            _routing.STRONG)

    return _runtime.Session(
        run_id=run_id, token=token,
        coordinator=_contract.build(
            run_id=run_id, generation=_run.read(run_id)["owner"]["generation"],
            actor_id="coordinator", invocation_id="coordinator-inv", worktree=tree,
            content_revision=_run.content_revision(tree), vendor=vendor,
            model="coordinator-session", effort=None, write_scope=_contract.WRITE_NONE,
            lease_token=None, permitted_tool_classes=(_contract.TOOL_READ,), provider_scope=tree,
            approval_mode="dontAsk", max_turns=30, wall_clock_seconds=900, orchestration=True),
        worktree=tree, vendor=vendor, config=_config.load())


def actor_factory(session: Any):
    """Names a new Actor without ever renaming an old one.

    The counter used to restart at zero on every invocation of this command, so the second day's
    first standard Actor was registered as `standard-1` again, over the top of the first day's. The
    registration overwrites vendor, model and effort, and older invocations still point at that
    identifier: change vendor or placement between units and the ledger silently reattributes
    finished work to a model that never ran it.
    """
    def make(capability: str) -> str:
        placement = _config.placement(session.config, session.vendor, capability)
        # Chosen and written in one transaction. Choosing here and registering afterwards left two
        # commands free to pick the same name from the same read.
        return _run.register_new_actor(
            session.run_id, session.token, prefix=capability, vendor=session.vendor,
            model=placement.model, effort=placement.effort, capability=capability)

    return make


def finish_command(held: "YoursToFinish") -> str:
    """The exact command that closes this reservation, addressed to the run it belongs to.

    Built from the session rather than from defaults. The first version printed only the invocation,
    so following it resolved the current directory and today's ledger: a reservation made with an
    explicit run, worktree or vendor could not be closed by the command the tool itself had printed,
    and even a default one stops matching after midnight. The global options come first because that
    is where this parser takes them.
    """
    return " ".join(shlex.quote(part) for part in (
        "python3", "scripts/staffing.py",
        "--worktree", str(held.session.worktree),
        "--vendor", held.session.vendor,
        "--run", held.session.run_id,
        "finish",
        "--invocation", held.prepared.invocation_id,
        "--completion", "observed",
        "--evidence", "<what you did>",
    ))


def shell_verifier(command: str, tree: Path):
    """A verification that is a command, because that is what a coordinator already has.

    Without `--verify` every unit settles `unverified`, which is honest and is not a failure. This
    exists so that the honest answer is not also the only one available to anyone who has not
    written Python.
    """
    def verify(prepared):
        del prepared
        finished = subprocess.run(command, shell=True, cwd=str(tree), capture_output=True,
                                  text=True)
        head = (finished.stdout or finished.stderr).strip().splitlines()
        return _runtime.Verification(
            finished.returncode == 0,
            f"`{command}` exited {finished.returncode}" + (f": {head[-1][:120]}" if head else ""))
    return verify


def staff(goal: str, *, worktree: str | Path | None = None, files: tuple[str, ...] = (),
          mutates: bool = False, mechanical: bool = False, fully_specified: bool = True,
          verify_command: str | None = None, vendor: str = "claude",
          unit_id: str | None = None, run_id: str | None = None, label: str | None = None,
          **signals: Any):
    """Staff one unit of work. The whole interface, for callers that are already in Python."""
    tree = worktree_of(worktree)
    run_id = run_id or default_run_id(tree)
    unit_id = unit_id or f"U{dt.datetime.now().strftime('%H%M%S')}"
    unit = unit_from(goal, unit_id=unit_id, files=files, mutates=mutates, mechanical=mechanical,
                     fully_specified=fully_specified, label=label, **signals)

    # Asked before any run exists, because the answer may be that no run should. This command is a
    # subprocess and cannot do the work itself; when routing says the session already running is
    # the cheapest sufficient Actor, the honest response is to say so and start nothing.
    #
    # Except when the unit writes. Then saying "you do it" and exiting leaves the worktree
    # unprotected: two callers both observe a free lease, both are told to proceed, and the
    # exclusion the whole feature rests on never happens. A free observation is not authority to
    # write afterwards. A mutating parent route therefore goes through `plan`, which takes the
    # lease and records the invocation, and the caller closes it with `finish`.
    _, decision = decide(tree, unit, vendor, _config.load())
    if decision.actor_id == COORDINATOR and not mutates:
        raise DoItYourself(decision.reason)

    session = open_session(tree, vendor, run_id)
    if decision.actor_id == COORDINATOR:
        prepared = _runtime.plan(session, unit, [
            _routing.Actor(COORDINATOR, coordinator_tier(), may_write=True, is_parent=True)])
        # The advisory decision was taken against a free worktree that another writer may have
        # taken since. A Plan that acquired nothing is not permission to write; it is the answer
        # that this is now the human's.
        if prepared.invocation_id is None or prepared.lease_token is None:
            raise ReservationLost(prepared.decision.reason)
        raise YoursToFinish(decision.reason, prepared, session)

    return _runtime.dispatch(
        session, unit, [], unit_goal=goal, files=files,
        verify=shell_verifier(verify_command, tree) if verify_command else None,
        actor_factory=actor_factory(session))


# --- the command line -----------------------------------------------------------------------


COORDINATOR = "coordinator"


def coordinator_tier() -> str | None:
    """What the calling session can do, or None when nobody has said.

    Derived rather than assumed. The first version handed every caller `STRONG`, which is not a fact
    about anybody: a cheap coordinator was told it was sufficient for work it is not, and an expert
    one was understated. Neither error is visible in the answer.

    `AB_COORDINATOR_MODEL` names the model this session runs on, and the operator's model ladder
    says which rung that model sits on. Both are recorded facts. With either missing the tier is
    unknown, the parent is not offered, and the delegation floor does not fire, which costs a
    cheaper route and invents nothing.
    """
    model = os.environ.get("AB_COORDINATOR_MODEL")
    if not model:
        return None
    try:
        ladder = _sibling("model-ladder")
        return ladder.rung(ladder.load(), os.environ.get("AB_COORDINATOR_VENDOR", "claude"), model)
    except Exception:
        return None


def decide(tree: Path, unit: Any, vendor: str, loaded: Any):
    """Who would do this unit, asked without creating anything.

    The session asking is offered as a parent Actor when its tier is known, because it is one.
    Leaving it out entirely made every unit look as though it needed somebody started, so the
    delegation floor could never fire and an unconfigured client got a refusal instead of the answer
    "you are sufficient, do it yourself". That was the entry point misrepresenting the situation
    rather than routing being wrong.
    """
    tier = coordinator_tier()
    candidates = ([_routing.Actor(COORDINATOR, tier, may_write=unit.mutates, is_parent=True)]
                  if tier else [])
    held = _lease.status(tree)["state"]
    return held, _routing.route(
        unit, candidates, _config.available_capabilities(loaded, vendor),
        lease_available=(held == "free"), preference=loaded.preference,
        limits=loaded.limits or None)


def _plan(args) -> int:
    """Who would do this, and why, without creating anything."""
    tree = worktree_of(args.worktree)
    loaded = _config.load()
    unit = unit_from(args.goal, unit_id="probe", files=tuple(args.files), mutates=args.mutates,
                     mechanical=args.mechanical, fully_specified=not args.underspecified)
    try:
        held, decision = decide(tree, unit, args.vendor, loaded)
    except _routing.RoutingError as refusal:
        # Said in full rather than as a mapping error, because the two ways out are different and
        # neither is obvious from the refusal itself.
        print(f"worktree   {tree}")
        print("route      nothing")
        print(f"because    {refusal}")
        if not coordinator_tier():
            print("           this session's own capability is not declared either, so it could "
                  "not be offered as a candidate.")
            print("           Set AB_COORDINATOR_MODEL to the model you are running on and record "
                  "that model in the ladder,")
            print("           or map this tier in staffing.json.")
        return 1
    if decision.actor_id == COORDINATOR:
        print(f"worktree   {tree}")
        print(f"lease      {held}")
        print("route      yourself")
        print(f"because    {decision.reason}")
        return 0
    print(f"lease      {held}")
    print(f"route      {decision.route}")
    print(f"capability {decision.capability or '-'}")
    if decision.capability and loaded.present:
        try:
            placement = _config.placement(loaded, args.vendor, decision.capability)
            print(f"would run  {placement.model}" + (f" at {placement.effort}"
                                                     if placement.effort else ""))
        except _config.ConfigError as error:
            print(f"would run  nothing: {error}")
    print(f"because    {decision.reason}")
    return 0


def _run_command(args) -> int:
    try:
        outcome = staff(args.goal, worktree=args.worktree, files=tuple(args.files),
                        mutates=args.mutates, mechanical=args.mechanical,
                        fully_specified=not args.underspecified, verify_command=args.verify,
                        vendor=args.vendor, unit_id=args.unit, run_id=args.run,
                        label=args.label)
    except DoItYourself as reason:
        print("route      yourself")
        print(f"because    {reason}")
        print("           nothing was started and nothing was recorded")
        return 0
    except ReservationLost as taken:
        # A refusal, not an instruction. The caller has no claim to this worktree.
        print("route      refused")
        print(f"because    the worktree was taken between deciding and preparing: {taken}")
        print("           nothing is reserved for you and nothing was recorded")
        return 2
    except YoursToFinish as held:
        print("route      yourself")
        print(f"because    {held}")
        print(f"lease      held for you, token {held.prepared.lease_token}")
        print(f"invocation {held.prepared.invocation_id}")
        print("           do the work, then close it:")
        print(f"           {finish_command(held)}")
        return 0
    tree = worktree_of(args.worktree)
    print(f"outcome    {outcome.state}")
    print(f"detail     {outcome.detail}")
    print(f"lease      {_lease.status(tree)['state']}")
    if _lease.status(tree)["state"] != "free":
        print("           the worktree is held; `staffing.py recover` after establishing that "
              "nothing is still writing")
    return 0 if outcome.state in (_run.SUCCEEDED, _run.UNVERIFIED) else 1


def _finish(args) -> int:
    """Close a unit the caller did itself, and give back the lease that belongs to it.

    Every identity is checked before anything is settled. The first version reconstructed the Plan
    as a parent route whatever the invocation actually was, and copied whichever token the worktree's
    lease happened to hold. A running read-only invocation can coexist with a writer in another run,
    so finishing the reader released the writer: the lease layer could not refuse it, because the
    token it was handed was genuinely valid for the lease it named.
    """
    tree = worktree_of(args.worktree)
    run_id = args.run or default_run_id(tree)
    state = _run.read(run_id)
    record = state["invocations"].get(args.invocation)
    if record is None:
        raise StaffingError(f"run {run_id} has no invocation {args.invocation}")
    if record["state"] != _run.RUNNING:
        print(f"invocation {args.invocation} already closed as "
              f"{record['outcome'] or record['state']}")
        return 0
    if record["actor_id"] != COORDINATOR:
        raise StaffingError(
            f"invocation {args.invocation} was run by {record['actor_id']}, not by you; only a "
            "unit routed back to the coordinator is finished this way, and a worker's completion "
            "is not yours to assert"
        )

    # The lease is taken from its own record and only when that record names this invocation. A
    # token is never borrowed from whoever currently holds one.
    held = _lease.status(tree)
    current = held.get("lease") or {}
    token = None
    if current:
        if (current.get("invocation_id") != args.invocation
                or current.get("run_id") != run_id):
            raise StaffingError(
                f"the worktree lease belongs to invocation {current.get('invocation_id')} of run "
                f"{current.get('run_id')}, not to {args.invocation} of {run_id}; refusing rather "
                "than releasing somebody else's exclusion"
            )
        token = current.get("token")

    session = open_session(tree, args.vendor, run_id)
    prepared = _runtime.Plan(
        _routing.Decision(_routing.ROUTE_ACTOR, record.get("capability"), COORDINATOR,
                          record["constraints_revision"]),
        args.invocation, None, None, token, _runtime.EXECUTION_PARENT)
    completion = {"observed": _lease.COMPLETION_OBSERVED,
                  "cooperative": _lease.COMPLETION_COOPERATIVE}.get(args.completion)

    # Evaluated here and passed as an observation. `settle` expects one; handing it the callback
    # meant the verification never ran at all and the reservation stayed open.
    observed = None
    if args.verify:
        try:
            observed = shell_verifier(args.verify, tree)(prepared)
        except BaseException as error:
            observed = _runtime.Verification(False, f"the verification command raised: {error}")

    outcome = _runtime.settle(
        session, prepared, exit_code=0 if args.completion != "failed" else 1, stdout="",
        evidence=args.evidence, completion=completion, verification=observed)
    print(f"outcome    {outcome.state}")
    print(f"lease      {_lease.status(tree)['state']}")
    return 0 if outcome.state in (_run.SUCCEEDED, _run.UNVERIFIED) else 1


def _status(args) -> int:
    tree = worktree_of(args.worktree)
    held = _lease.status(tree)
    print(f"worktree   {tree}")
    print(f"lease      {held['state']}")
    if held["state"] == "uncertain":
        print(f"reason     {held['uncertain']['reason']}")
        print(f"invocation {held['uncertain']['invocation_id']}")
    run_id = args.run or default_run_id(tree)
    try:
        state = _run.read(run_id)
    except _run.RunError:
        print(f"run        {run_id}: none yet")
        return 0
    print(f"run        {run_id}")
    for record in sorted(state["invocations"].values(), key=lambda r: r["started_at"]):
        actor = state["actors"].get(record["actor_id"], {})
        described = state["units"].get(record["unit_id"], {}).get("label") or record["unit_id"]
        effort = f" at {actor['effort']}" if actor.get("effort") else ""
        print(f"  {record['outcome'] or record['state']:<11} "
              f"{(actor.get('model') or '-') + effort:<22} attempt {record['attempt']}  "
              f"{described}")
    print(f"unresolved {_run.unresolved(run_id) or 'none'}")
    return 0


def _recover(args) -> int:
    """Reopen a held worktree, on evidence, and close the invocation behind it.

    Everything is resolved and validated before anything is cleared. The first version cleared the
    lease, then discovered it needed a terminal finding, then told the caller to rerun, and the
    rerun returned immediately because the lease was already free. The second validated the targets
    it worked out for itself and none of the ones the caller supplied, so an explicit `--run` and a
    nonexistent `--invocation` cleared a real lease and reconciled nothing.
    """
    tree = worktree_of(args.worktree)
    held = _lease.status(tree)
    uncertain = held["uncertain"] if held["state"] == "uncertain" else None
    stranded = uncertain["invocation_id"] if uncertain else None

    run_id = args.run or (uncertain.get("run_id") if uncertain else None) or default_run_id(tree)

    state = None
    try:
        state = _run.read(run_id)
    except _run.RunError as error:
        # An absent ledger and a damaged one are different facts. Swallowing both and carrying on
        # to clear a lease treats "cannot read this" as "there is nothing here".
        if "does not exist" not in str(error):
            raise StaffingError(f"run {run_id} cannot be read: {error}") from error

    if state is not None and Path(state["worktree"]).resolve() != tree:
        raise StaffingError(
            f"run {run_id} belongs to {state['worktree']}, not to {tree}")

    open_work = _run.unresolved(run_id) if state else []
    target = args.invocation or stranded or (open_work[0] if len(open_work) == 1 else None)

    # Every explicit target is checked against what actually exists, and against what the lease
    # itself names. A caller naming somebody else's work must not reach a clearing step.
    if args.invocation:
        if state is None or args.invocation not in state["invocations"]:
            raise StaffingError(f"run {run_id} has no invocation {args.invocation}")
        if stranded and args.invocation != stranded:
            raise StaffingError(
                f"the worktree lease was held by invocation {stranded}; recovering "
                f"{args.invocation} would clear an exclusion that is not its own"
            )
    if args.run and stranded and uncertain.get("run_id") not in (None, run_id):
        raise StaffingError(
            f"the worktree lease belongs to run {uncertain['run_id']}, not {run_id}")

    if stranded is None and not open_work:
        print(f"the worktree is {held['state']} and run {run_id} has nothing unresolved")
        return 0
    if open_work and target is None:
        print(f"run {run_id} has {len(open_work)} unresolved invocations; name one with "
              "--invocation")
        return 1
    if target in open_work and not args.finding:
        print(f"invocation {target} is unresolved and needs a terminal finding; rerun with "
              f"--finding from {list(_run.TERMINAL_OUTCOMES)}. Nothing has been changed.")
        return 1

    if stranded is not None:
        _lease.clear_uncertain(tree, stranded, args.evidence)
        print(f"lease      {_lease.status(tree)['state']}")
    if state and target in open_work:
        _run.reconcile(run_id, state["owner"]["token"], target, args.finding, args.evidence)
        print(f"invocation {target} reconciled as {args.finding}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--worktree", default=None)
    parser.add_argument("--vendor", default="claude")
    parser.add_argument("--run", default=None, help="which ledger; defaults to worktree and date")
    commands = parser.add_subparsers(dest="command", required=True)

    for name in ("plan", "run"):
        sub = commands.add_parser(name)
        sub.add_argument("--goal", required=True)
        sub.add_argument("--files", nargs="*", default=[])
        sub.add_argument("--mutates", action="store_true", help="this unit writes to the worktree")
        sub.add_argument("--mechanical", action="store_true",
                         help="no judgement needed; routes cheaper")
        sub.add_argument("--underspecified", action="store_true",
                         help="the goal is not yet precise; routes dearer")
        if name == "run":
            sub.add_argument("--verify", default=None,
                             help="a shell command; without it the unit settles unverified")
            sub.add_argument("--unit", default=None, help="the unit's identifier; retries reuse it")
            sub.add_argument("--label", default=None,
                             help="a few words for the ledger; defaults to the goal's first line")

    commands.add_parser("status")

    finish = commands.add_parser("finish")
    finish.add_argument("--invocation", required=True)
    finish.add_argument("--evidence", required=True, help="what you did")
    finish.add_argument("--completion", default="observed",
                        choices=("observed", "cooperative", "failed"),
                        help="observed when you waited for every writer you started")
    finish.add_argument("--verify", default=None, help="a shell command, as for run")

    recover = commands.add_parser("recover")
    recover.add_argument("--evidence", required=True,
                         help="what you established about nothing still writing")
    recover.add_argument("--finding", default=None, choices=list(_run.TERMINAL_OUTCOMES))
    recover.add_argument("--invocation", default=None,
                         help="which one, when more than one is open")

    args = parser.parse_args(argv)
    try:
        return {"plan": _plan, "run": _run_command, "finish": _finish,
                "status": _status, "recover": _recover}[args.command](args)
    except (StaffingError, _run.RunError, _lease.LeaseError, _config.ConfigError,
            _routing.RoutingError, _runtime.RuntimeError_) as error:
        print(f"staffing: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
