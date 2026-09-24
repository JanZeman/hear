#!/usr/bin/env python3
"""The authoritative run state for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Exactly one coordinator commits transitions for a run, every invocation is recorded before it
    is launched and resolved only by an observed outcome, and an invocation whose effects are
    unknown is never redispatched on the runtime's own initiative.

Three distinctions do the work, and collapsing any of them is how this goes wrong:

1. **Actor identity, provider handle, and running process are three things.** An Actor persists
   across invocations; a provider handle is one vendor conversation that may or may not be
   resumable; a process exists only while a single invocation runs. The document names them
   separately so a dead process is never read as a dead Actor.

2. **Atomic replacement prevents torn files, not lost updates.** A writer that reads, decides, and
   replaces can still overwrite a concurrent decision. Ownership is therefore enforced by a token
   checked inside a serialized critical section, not by convention, and that includes the recovery
   takeover path where a new coordinator adopts a run whose owner died.

3. **An uncertain outcome is its own state.** Not success, not failure, and not an invitation to
   try again automatically. A crash between launching a writer and recording its result leaves work
   whose effects nobody observed; the honest response is to say so and stop, because the alternative
   is replaying a mutation that may already have landed. A human-directed retry is welcome and gets
   a fresh invocation identity, with the uncertain outcome retained rather than overwritten.
"""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
from typing import Any


def _sibling(name: str):
    """Load a script living beside this one.

    Every consumer in this repository loads modules by file path, so a plain import would only work
    when the scripts directory happens to be on `sys.path`. Registering the module is what lets the
    loaded module's own machinery resolve itself.
    """
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
_contract = _sibling("staffing_contract")

SCHEMA_VERSION = 1

# Terminal outcomes an invocation may reach. `uncertain` is deliberately one of them rather than an
# absence of one: work whose effects were never observed has a state, and that state is not failure.
SUCCEEDED = "succeeded"
FAILED = "failed"
UNCERTAIN = "uncertain"
# An Actor said it finished, the process ended cleanly, and nobody checked. That is not success and
# it is not uncertainty: the effects are known, the result is not. Collapsing it into `succeeded`
# was the runtime promoting a self-report to a verified outcome; collapsing it into `uncertain`
# would close the worktree over work that is probably fine.
UNVERIFIED = "unverified"
TERMINAL_OUTCOMES = (SUCCEEDED, FAILED, UNCERTAIN, UNVERIFIED)

RUNNING = "running"


class RunError(RuntimeError):
    """Every refusal in this module. All of them fail closed."""


class BudgetExhausted(RunError):
    """A cap was reached at the moment of spending, rather than at the moment of checking.

    Distinguished from every other refusal because it is not an error in the run: it is the answer
    the routing rules would have given had they read the counter a moment later. The caller routes
    the work to the human, which is what a reached cap means everywhere else in this feature.
    """


def runs_dir() -> Path:
    return _lease.state_dir().parent / "runs"


def _paths(run_id: str) -> tuple[Path, Path]:
    if not run_id or "/" in run_id or run_id.startswith("."):
        raise RunError(f"invalid run id: {run_id!r}")
    directory = runs_dir()
    return directory / f"{run_id}.lock", directory / f"{run_id}.json"


def _now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def _status_entries(root: Path) -> list[tuple[str, str, str | None]]:
    """`(xy, path, rename_source)` for every path that differs from HEAD, in a stable order.

    Two details of porcelain `-z` decide whether this is right or merely plausible.

    `--untracked-files=all` is not the default. Without it a new directory is reported as the single
    entry `?? new-dir/` and nothing under it is ever named, so two different versions of
    `new-dir/file.py` produced the same revision. Creating a directory and then editing what is in
    it is the ordinary shape of new work, not an exotic case.

    A rename carries its source as a *bare* following field, with no XY prefix. Sorting the fields
    before parsing them therefore fed a filename into a parser that assumed a prefix and chopped its
    first three characters off. The pair is consumed here, in order, and the sort happens after.
    """
    status = subprocess.run(
        ["git", "-C", str(root), "status", "--porcelain=v1", "-z", "--untracked-files=all"],
        capture_output=True, text=True, check=False)
    if status.returncode:
        raise RunError(f"cannot read the worktree status of {root}")

    fields = [part for part in status.stdout.split("\0") if part]
    entries: list[tuple[str, str, str | None]] = []
    index = 0
    while index < len(fields):
        field = fields[index]
        xy, path = field[:2], field[3:]
        source = None
        if "R" in xy or "C" in xy:
            index += 1
            source = fields[index] if index < len(fields) else ""
        entries.append((xy, path, source))
        index += 1
    entries.sort(key=lambda entry: (entry[1], entry[0], entry[2] or ""))
    return entries


def _index_blobs(root: Path, paths: list[str]) -> dict[str, str]:
    """The object id the index holds for each path, which is not derivable from the working tree.

    Staging a file and then editing it again leaves two different contents under one path, and only
    one of them is on disk. This is also what represents a submodule: its index entry is the commit
    the pointer names, so a moved submodule changes the revision without this function having to
    descend into it.
    """
    if not paths:
        return {}
    listed = subprocess.run(
        ["git", "-C", str(root), "ls-files", "--stage", "-z", "--", *paths],
        capture_output=True, text=True, check=False)
    if listed.returncode:
        # Fails closed. Returning an empty mapping made every path read as unstaged, so the
        # fingerprint still came back looking ordinary while silently dropping the index from its
        # coverage, and two different staged states became indistinguishable again. An unavailable
        # answer is not the answer "nothing is staged".
        raise RunError(
            f"cannot read the index of {root} while identifying worktree content "
            f"(git ls-files exited {listed.returncode}): {listed.stderr.strip()}"
        )
    blobs: dict[str, str] = {}
    for record in listed.stdout.split("\0"):
        if not record or "\t" not in record:
            continue
        meta, path = record.split("\t", 1)
        parts = meta.split()
        if len(parts) >= 2:
            blobs[path] = f"{parts[0]}:{parts[1]}"
    return blobs


def content_revision(worktree: Path) -> str:
    """Identify what the worktree currently *contains*, down to the bytes.

    A reviewer that inspected a checkout and a writer that changed it can share a HEAD and disagree
    about every file, so HEAD alone cannot tell a moved worktree from a still one. The first version
    folded in `git status` and stopped there, which was not far enough: porcelain output names
    *which* paths differ from HEAD and says nothing about how. Two successive edits to a file that
    was already modified produce identical status lines, and so produced an identical revision,
    which let a stale result describe content that had moved twice underneath it.

    So the changed paths are enumerated from status and then read. Only they: a clean tracked file
    is already identified by HEAD, and hashing the whole tree would make this too expensive to call
    at every invocation, which is exactly when it needs calling.

    What this covers, stated rather than implied, because a claim of "down to the bytes" that is
    wider than the implementation is worse than a narrow one:

    - every tracked file that matches HEAD, through HEAD itself;
    - every changed or untracked file, through its bytes, enumerated individually and not as a
      collapsed parent directory;
    - every changed symlink, through its target, which is the whole of its content;
    - staged content and submodule pointers, through the object id in the index, which the working
      tree does not show;
    - renames, through both names.

    What it deliberately excludes, and what that costs: ignored files, and the contents of a
    submodule beyond the commit its pointer names. Neither exclusion is safe by definition. An
    ignored path can be a real input, since build output, generated code, and local configuration
    are all routinely ignored and all routinely read. The boundary is therefore a stated limit
    rather than a proof: where a unit's work depends on an excluded input, the staleness this
    function detects will not cover it, and that unit needs verification that looks at the input
    directly.
    """
    root = _lease.canonical_worktree(worktree)
    head = subprocess.run(["git", "-C", str(root), "rev-parse", "HEAD"],
                          capture_output=True, text=True, check=False)
    entries = _status_entries(root)
    blobs = _index_blobs(root, [path for _, path, _ in entries if path])

    digest = hashlib.sha256()
    digest.update((head.stdout.strip() or "no-commit").encode("utf-8"))
    for xy, path, source in entries:
        digest.update(b"\0")
        digest.update(f"{xy} {path}".encode("utf-8", "surrogateescape"))
        if source is not None:
            digest.update(b"\0from\0")
            digest.update(source.encode("utf-8", "surrogateescape"))
        digest.update(b"\0index\0")
        digest.update(blobs.get(path, "unstaged").encode("utf-8"))
        target = root / path
        try:
            if target.is_symlink():
                # Checked before `is_file`, which follows the link and would hash the target's
                # bytes while a repointed link went unnoticed.
                digest.update(b"\0link\0")
                digest.update(os.readlink(target).encode("utf-8", "surrogateescape"))
            elif target.is_file():
                with target.open("rb") as handle:
                    for chunk in iter(lambda: handle.read(65536), b""):
                        digest.update(chunk)
            else:
                # A path just deleted, or a submodule directory. Its status entry and index blob are
                # the whole of what can be said here, and both are already folded in above.
                digest.update(b"\0no-readable-bytes")
        except OSError as error:
            raise RunError(f"cannot read {path} while identifying worktree content: {error}")
    return digest.hexdigest()[:24]


class _Document:
    """Read-modify-write of one run document inside a serialized critical section."""

    def __init__(self, run_id: str):
        self.run_id = run_id
        self.lock_path, self.path = _paths(run_id)

    def __enter__(self) -> "_Document":
        lock_dir = self.lock_path.parent
        try:
            lock_dir.mkdir(parents=True, exist_ok=True)
            os.chmod(lock_dir, 0o700)
        except OSError as error:
            raise RunError(
                f"cannot prepare the staffing run directory {lock_dir}: {error}. "
                "Set AB_STAFFING_STATE_HOME to a directory you can write and staffing will keep "
                "all of its state beside it; the default lives under XDG_STATE_HOME, which some "
                "machines own as root."
            )
        self._handle = self.lock_path.open("a+", encoding="utf-8")
        try:
            os.chmod(self.lock_path, 0o600)
            _lease.fcntl.flock(self._handle.fileno(), _lease.fcntl.LOCK_EX)
            # Reading can refuse, on a corrupt document. `__exit__` never runs for an `__enter__`
            # that raises, so the lock and the handle have to be given back here or a single
            # damaged run would wedge every later attempt to inspect it.
            self.data = self._read()
        except BaseException:
            _lease.fcntl.flock(self._handle.fileno(), _lease.fcntl.LOCK_UN)
            self._handle.close()
            raise
        return self

    def __exit__(self, *exc):
        _lease.fcntl.flock(self._handle.fileno(), _lease.fcntl.LOCK_UN)
        self._handle.close()
        return False

    def _read(self) -> dict[str, Any] | None:
        try:
            return _lease._read_json(self.path)
        except _lease.LeaseError as error:
            # A damaged run document is not a missing one: treating it as missing would hand a new
            # coordinator a clean slate over work whose state nobody knows.
            raise RunError(str(error)) from error

    def require(self) -> dict[str, Any]:
        if self.data is None:
            raise RunError(f"run {self.run_id} does not exist")
        return self.data

    def authorize(self, token: str) -> dict[str, Any]:
        data = self.require()
        if data["owner"]["token"] != token:
            raise RunError(
                "this token does not own the run; ownership moved, which means another "
                "coordinator has taken over and this one must stop writing"
            )
        return data

    def commit(self, data: dict[str, Any]) -> None:
        data["revision"] = data.get("revision", 0) + 1
        data["updated_at"] = _now()
        _lease._write_json_atomically(self.path, data)
        self.data = data


def create(run_id: str, coordinator_id: str, worktree: Path,
           coordinator_actor_id: str | None = None) -> str:
    """Start a run and return the ownership token. Refuses to adopt an existing run silently.

    `coordinator_actor_id` names which Actor may dispatch in this run. It belongs in the document
    from the first write: orchestration authority is checked against this record rather than against
    a contract's claim about itself, so a run that acquired its coordinator later would have a
    window in which that check had nothing to read.
    """
    # Checked here, on the operation, rather than left as a predicate a caller may remember to
    # invoke. A guard with no production caller is decorative, and this one had none: a dispatched
    # child could reach the ordinary create API and coordinate a run of its own, which is the exact
    # bypass the invocation contract exists to close.
    _contract.require_fresh_run_authority()
    root = _lease.canonical_worktree(worktree)
    with _Document(run_id) as document:
        if document.data is not None:
            raise RunError(
                f"run {run_id} already exists and is owned by "
                f"{document.data['owner']['coordinator_id']}; take it over explicitly instead"
            )
        token = secrets.token_hex(16)
        document.commit({
            "schema_version": SCHEMA_VERSION,
            "run_id": run_id,
            "worktree": str(root),
            "created_at": _now(),
            "owner": {"coordinator_id": coordinator_id, "token": token, "generation": 1,
                      "adopted_at": _now()},
            "coordinator_actor_id": coordinator_actor_id or coordinator_id,
            "actors": {},
            "invocations": {},
            "units": {},
        })
        return token


def take_ownership(run_id: str, coordinator_id: str, evidence: str) -> str:
    """Adopt a run whose coordinator is gone. Explicit, evidenced, and it invalidates the old token.

    Recovery is a real operation rather than a silent side effect of another coordinator starting,
    because two live coordinators writing one run is precisely what ownership exists to prevent.
    """
    if not evidence.strip():
        raise RunError(
            "taking over a run requires recorded evidence that the previous coordinator stopped"
        )
    with _Document(run_id) as document:
        data = document.require()
        previous = data["owner"]
        data["owner"] = {
            "coordinator_id": coordinator_id,
            "token": secrets.token_hex(16),
            "generation": previous.get("generation", 1) + 1,
            "adopted_at": _now(),
            "adopted_from": previous.get("coordinator_id"),
            "evidence": evidence.strip(),
        }
        document.commit(data)
        return data["owner"]["token"]


def register_actor(run_id: str, token: str, actor_id: str, vendor: str, model: str,
                   effort: str | None, capability: str) -> None:
    with _Document(run_id) as document:
        data = document.authorize(token)
        data["actors"][actor_id] = {
            "vendor": vendor, "model": model, "effort": effort, "capability": capability,
            # The provider handle is filled in only when a vendor actually gives one, and it is
            # separate from the Actor because an Actor outlives any single conversation.
            "provider_handle": data["actors"].get(actor_id, {}).get("provider_handle"),
            "registered_at": _now(),
        }
        document.commit(data)


def register_new_actor(run_id: str, token: str, *, prefix: str, vendor: str, model: str,
                       effort: str | None, capability: str) -> str:
    """Allocate an unused Actor identifier and register it, in one transaction.

    Choosing the name in the caller and registering it afterwards leaves a window: two commands from
    the same coordinator read the same set of Actors, both pick `cheap-1`, and the second
    registration overwrites the first while earlier invocations still point at that identifier.
    Read-only dispatches take no write lease, so nothing else serializes them.

    The name is chosen and written under the run document's own lock, which is the serialization
    this already has rather than another one.
    """
    with _Document(run_id) as document:
        data = document.authorize(token)
        index = 1
        while f"{prefix}-{index}" in data["actors"]:
            index += 1
        actor_id = f"{prefix}-{index}"
        data["actors"][actor_id] = {
            "vendor": vendor, "model": model, "effort": effort, "capability": capability,
            "provider_handle": None, "registered_at": _now(),
        }
        document.commit(data)
        return actor_id


def begin_invocation(run_id: str, token: str, *, invocation_id: str, unit_id: str, actor_id: str,
                     constraints_revision: str, content_revision_value: str,
                     attempt_cap: int | None = None, label: str | None = None) -> None:
    """Record an invocation *before* it is launched, reserving the attempt in the same breath.

    This ordering is the whole point. A crash between this call and the launch leaves a record of
    work that may not have started; a crash the other way around would leave work nobody knows ran.
    Only the first of those is recoverable.

    `attempt_cap` is checked here rather than trusted from the caller, because the caller read the
    counter outside this lock. Two dispatches could both read the last permitted attempt, both pass
    the routing check, and both record one; or one could read, pause, and resume after another had
    spent the last allowance. Checking and reserving in one critical section is the only version of
    this that is a cap rather than a suggestion.
    """
    with _Document(run_id) as document:
        data = document.authorize(token)
        if attempt_cap is not None:
            spent = len(data["units"].get(unit_id, {}).get("attempts", []))
            if spent >= attempt_cap:
                raise BudgetExhausted(
                    f"attempt cap reached ({spent} of {attempt_cap} used) for unit {unit_id}; "
                    "this goes to the human rather than to a more expensive Actor"
                )
        if actor_id not in data["actors"]:
            raise RunError(f"actor {actor_id} is not registered in run {run_id}")
        if invocation_id in data["invocations"]:
            raise RunError(f"invocation {invocation_id} already exists")
        blocking = _unresolved_for_unit(data, unit_id)
        if blocking:
            raise RunError(
                f"unit {unit_id} has an unresolved invocation {blocking}; reconcile it before "
                "dispatching again, because its effects on the worktree were never observed"
            )
        live = [
            identifier for identifier, record in data["invocations"].items()
            if record["actor_id"] == actor_id and record["state"] == RUNNING
        ]
        if live:
            raise RunError(
                f"actor {actor_id} already has invocation {live[0]} running; invocations are "
                "serialized per Actor so two of them cannot resume one conversation at once"
            )
        data["invocations"][invocation_id] = {
            "invocation_id": invocation_id,
            "unit_id": unit_id,
            "actor_id": actor_id,
            "state": RUNNING,
            "outcome": None,
            "provider_handle": None,
            "constraints_revision": constraints_revision,
            "content_revision": content_revision_value,
            "started_at": _now(),
            "attempt": len(data["units"].get(unit_id, {}).get("attempts", [])) + 1,
        }
        unit = data["units"].setdefault(unit_id, {"unit_id": unit_id, "attempts": []})
        # Kept on the unit rather than the invocation, because retries of one unit are one piece of
        # work. First writer wins: a later attempt that describes itself differently is describing
        # the same thing, and renaming it mid-run would make the ledger harder to read, not easier.
        if label and not unit.get("label"):
            unit["label"] = label[:120]
        unit["attempts"].append(invocation_id)
        document.commit(data)


def record_provider_handle(run_id: str, token: str, invocation_id: str, handle: str) -> None:
    """Record the vendor's conversation identifier, so a recoverable conversation is not orphaned.

    The narrower claim is the true one. This is called once the vendor process has returned and its
    output can be read, not at the moment the identifier comes into existence: agent-base does not
    consume a vendor's event stream while it runs. An invocation that times out or is killed
    therefore has no handle here even though the vendor had one, which is why an uncertain
    invocation is reconciled by looking at the worktree rather than by resuming a conversation.
    """
    with _Document(run_id) as document:
        data = document.authorize(token)
        record = data["invocations"].get(invocation_id)
        if record is None:
            raise RunError(f"invocation {invocation_id} does not exist")
        record["provider_handle"] = handle
        data["actors"][record["actor_id"]]["provider_handle"] = handle
        document.commit(data)


def record_addressing(run_id: str, token: str, invocation_id: str, addressed: bool) -> None:
    """Whether the Actor named this invocation or only its unit.

    Persisted because it was claimed to be auditable and was not: the parsed envelope knew, the
    ledger did not, and by the time anybody asked the envelope was gone. A weakly addressed result
    was accepted on the strength of the transport binding the answer to the request, and a reader
    reconstructing that later needs to see which ones those were.
    """
    with _Document(run_id) as document:
        data = document.authorize(token)
        record = data["invocations"].get(invocation_id)
        if record is None:
            raise RunError(f"invocation {invocation_id} does not exist")
        record["addressed_by_invocation"] = bool(addressed)
        document.commit(data)


def complete_invocation(run_id: str, token: str, invocation_id: str, outcome: str,
                        evidence: str = "", result_content_revision: str | None = None) -> None:
    """Resolve an invocation to one of the three terminal outcomes.

    A result produced against a worktree that has since moved is not accepted as `succeeded`: it is
    downgraded to `uncertain`, because a verification bound to content that no longer exists proves
    nothing about what is there now.
    """
    if outcome not in TERMINAL_OUTCOMES:
        raise RunError(f"outcome must be one of {TERMINAL_OUTCOMES}, not {outcome!r}")
    with _Document(run_id) as document:
        data = document.authorize(token)
        record = data["invocations"].get(invocation_id)
        if record is None:
            raise RunError(f"invocation {invocation_id} does not exist")
        if record["state"] != RUNNING:
            raise RunError(
                f"invocation {invocation_id} already resolved as {record['outcome']}; a terminal "
                "outcome is kept rather than overwritten"
            )
        final = outcome
        stale = (
            outcome == SUCCEEDED
            and result_content_revision is not None
            and result_content_revision != record["content_revision"]
        )
        if stale:
            final = UNCERTAIN
            evidence = (evidence + " ").strip() + (
                f"[downgraded: result described content {result_content_revision}, invocation ran "
                f"against {record['content_revision']}]"
            )
        record.update({"state": final, "outcome": final, "evidence": evidence,
                       "finished_at": _now()})
        document.commit(data)


def _unresolved_for_unit(data: dict, unit_id: str) -> str | None:
    for identifier in data["units"].get(unit_id, {}).get("attempts", []):
        record = data["invocations"][identifier]
        if record["state"] == RUNNING or record["outcome"] == UNCERTAIN:
            if not record.get("reconciled_at"):
                return identifier
    return None


def reconcile(run_id: str, token: str, invocation_id: str, finding: str, evidence: str) -> None:
    """Close out an invocation nobody watched finish, using what an operator actually established.

    A `running` record left by a crash becomes `uncertain` unless evidence says otherwise. Marking
    it reconciled is what permits a further attempt on that unit, and it is deliberately a separate,
    evidenced step rather than something a restart does on its own.
    """
    if finding not in TERMINAL_OUTCOMES:
        raise RunError(f"finding must be one of {TERMINAL_OUTCOMES}, not {finding!r}")
    if not evidence.strip():
        raise RunError("reconciling an unresolved invocation requires recorded evidence")
    with _Document(run_id) as document:
        data = document.authorize(token)
        record = data["invocations"].get(invocation_id)
        if record is None:
            raise RunError(f"invocation {invocation_id} does not exist")
        if record["state"] == RUNNING:
            record["finished_at"] = _now()
        record["state"] = finding
        # The original outcome is retained beside the reconciliation, so the history says both what
        # the runtime observed and what a human later established.
        record["reconciled"] = {"finding": finding, "evidence": evidence.strip(), "at": _now(),
                                "previous_outcome": record["outcome"]}
        record["outcome"] = finding
        record["reconciled_at"] = _now()
        document.commit(data)


def record_escalation(run_id: str, token: str, unit_id: str, *,
                      consultation_cap: int | None = None,
                      level_cap: int | None = None) -> None:
    """Count one consultation against the unit, durably, reserving it against its caps.

    Counters kept only in a caller's object survive exactly as long as that object. A fresh `Unit`
    after each resolved failure reset every budget, which is how a cap meant to stop repeated
    spending stopped nothing.

    The caps are re-checked here for the same reason the attempt cap is: the routing check read
    these counters outside this lock, so two escalations could both see the final available
    consultation, both pass, and both increment it.
    """
    with _Document(run_id) as document:
        data = document.authorize(token)
        unit = data["units"].setdefault(unit_id, {"unit_id": unit_id, "attempts": []})
        if consultation_cap is not None and unit.get("consultations", 0) >= consultation_cap:
            raise BudgetExhausted(
                f"consultation cap reached ({unit.get('consultations', 0)} of {consultation_cap} "
                f"asked) for unit {unit_id}"
            )
        if level_cap is not None and unit.get("escalation_level", 0) >= level_cap:
            raise BudgetExhausted(
                f"escalation cap reached (level {unit.get('escalation_level', 0)}) for unit "
                f"{unit_id}"
            )
        unit["consultations"] = unit.get("consultations", 0) + 1
        unit["escalation_level"] = unit.get("escalation_level", 0) + 1
        document.commit(data)


def unit_counters(state: dict, unit_id: str) -> dict[str, int]:
    """What this unit has actually spent, according to the record rather than the caller.

    Attempts are derived rather than stored: the invocation ledger already is the list of dispatches,
    so a second place to count them could only ever disagree with it.
    """
    unit = state.get("units", {}).get(unit_id, {})
    return {
        "attempts": len(unit.get("attempts", [])),
        "consultations": unit.get("consultations", 0),
        "escalation_level": unit.get("escalation_level", 0),
    }


def unresolved(run_id: str) -> list[str]:
    """Every invocation whose effects nobody observed. A run with any of these is not finished."""
    with _Document(run_id) as document:
        data = document.require()
        return [
            identifier for identifier, record in data["invocations"].items()
            if (record["state"] == RUNNING or record["outcome"] == UNCERTAIN)
            and not record.get("reconciled_at")
        ]


def retire(run_id: str, token: str) -> None:
    """Remove local run state, and only for a run that actually reached a reconciled end.

    "End of run" means a reconciled terminal run with nothing unresolved. It does not mean process
    exit, and it never means a `finally` block: durable intent belongs in a session snapshot before
    this is called, and anything unresolved stays exactly where it is.
    """
    with _Document(run_id) as document:
        document.authorize(token)
        outstanding = [
            identifier for identifier, record in document.data["invocations"].items()
            if (record["state"] == RUNNING or record["outcome"] == UNCERTAIN)
            and not record.get("reconciled_at")
        ]
        if outstanding:
            raise RunError(
                f"run {run_id} still has unresolved invocations {outstanding}; reconcile them "
                "before retiring its state, or the record of what nobody observed is simply lost"
            )
        document.path.unlink(missing_ok=True)


def read(run_id: str) -> dict:
    with _Document(run_id) as document:
        return document.require()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Inspect a staffing run's authoritative state.")
    parser.add_argument("command", choices=("show", "unresolved"))
    parser.add_argument("run_id")
    args = parser.parse_args(argv)
    try:
        if args.command == "unresolved":
            for identifier in unresolved(args.run_id):
                print(identifier)
            return 0
        print(json.dumps(read(args.run_id), indent=2, sort_keys=True))
        return 0
    except (RunError, _lease.LeaseError) as error:
        print(f"Staffing run: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
