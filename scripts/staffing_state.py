#!/usr/bin/env python3
"""The shared task state for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    An Actor joining later can act correctly from this state alone, and nothing that could act on
    its own survives the trip into a durable record.

Two ideas shape it.

**This is memory, not history.** Persistent Actors do not share a conversation, and keeping them in
step by copying every message to everyone would spend more than the arrangement saves. What they
need is the small set of things a competent newcomer would have to be told: what is being attempted,
what has been decided and why, what was tried and rejected, what constraints are live, and what is
still open. Writing is therefore gated on a named boundary rather than left to judgement, because a
state file anyone may append to at any time becomes a transcript within an afternoon, and a
transcript is the thing this exists instead of.

**Durability goes through the existing mechanism.** Live state is untracked operational memory with
a defined end. When the work reaches a handoff it is distilled into an ordinary `.agents/sessions/`
snapshot under `AB-SESSION-001`, which is already portable across machines and vendors, already
reviewed in Git, and already pruned safely. There is no second durable store. What the distillation
deliberately drops is everything that could act: lease tokens, ownership tokens, and provider
handles. A snapshot is intent, and intent that silently resurrects a lock or a live vendor session
on another machine is a trap rather than a handoff.
"""

from __future__ import annotations

import datetime as dt
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
from typing import Any


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

SCHEMA_VERSION = 1

# The sections, in the order a newcomer needs them. Fixed rather than free-form: an open structure
# invites a running commentary, and the value here is entirely in what was left out.
SECTIONS = (
    "goal",
    "phase",
    "plan",
    "decisions",
    "discoveries",
    "constraints",
    "rejected",
    "open_questions",
)

# Writing is allowed only at one of these. "I just learned something" is not on the list; it becomes
# a discovery when it changes what someone would do.
BOUNDARIES = (
    "decision_made",
    "constraint_discovered",
    "unit_completed",
    "hypothesis_rejected",
    "escalation_triggered",
    "verification_failed",
    "phase_changed",
)

# Fields that may never reach a durable snapshot, because each of them can act.
ACTING_FIELDS = ("token", "lease_token", "provider_handle", "ownership_token")


class StateError(RuntimeError):
    """Every refusal here. All of them fail closed."""


def _path(run_id: str) -> Path:
    if not run_id or "/" in run_id or run_id.startswith("."):
        raise StateError(f"invalid run id: {run_id!r}")
    return _lease.state_dir().parent / "state" / f"{run_id}.json"


class _Transaction:
    """One serialized, owner-authorized read-modify-write of the shared state.

    Two defects lived in the absence of this, and neither needed a malicious participant.

    Without a lock, two coordinators could load the same state, each append its own entry, and each
    save; the second save silently dropped the first entry. The file was written atomically, which
    protects a reader from seeing half a document and does nothing at all for a lost update: those
    are different properties and the first was mistaken for the second.

    Without ownership, a coordinator whose run had already been taken over could still write here.
    The run document has refused stale writers from the beginning, and this file, which is what a
    joining Actor is briefed from, refused nobody.

    Ownership is validated and the state is committed inside **one** hold of the run's own lock, not
    two. The first version read the owner through `_run.read`, which takes that lock and gives it
    back before returning, leaving a window: an old coordinator could pass its token check, a
    takeover could rotate the owner, and the old coordinator could then commit with authority it no
    longer had. Re-reading the token just before saving would only narrow that window; holding the
    lock across the check and the commit closes it, because `take_ownership` needs the same lock and
    therefore waits.

    Lock order is state lock, then run lock, and it is the only order used anywhere: nothing in
    `staffing_run` reaches for the state lock. The body between them must not call back into
    `staffing_run`, which would block on a lock this transaction already holds; it edits a plain
    dictionary, and that is the reason it does nothing else.
    """

    def __init__(self, run_id: str, token: str):
        self.run_id = run_id
        self.token = token
        self.path = _path(run_id)
        self.lock_path = self.path.with_suffix(".lock")

    def __enter__(self) -> dict[str, Any]:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        os.chmod(self.path.parent, 0o700)
        self._handle = None
        self._run_document = None
        try:
            self._handle = self.lock_path.open("a+", encoding="utf-8")
            os.chmod(self.lock_path, 0o600)
            _lease.fcntl.flock(self._handle.fileno(), _lease.fcntl.LOCK_EX)
            document = _run._Document(self.run_id)
            # Entered before it is registered. A `_Document.__enter__` that fails has already given
            # back its own lock and closed its own handle, so calling `__exit__` on it afterwards
            # raises on the closed descriptor. That masked the real error, which was usually a
            # corrupt run document, and skipped the release of *this* lock, wedging every later
            # write behind a transaction that had already failed.
            document.__enter__()
            self._run_document = document
            try:
                document.authorize(self.token)
            except _run.RunError as error:
                raise StateError(
                    f"{error}; this coordinator must stop writing shared state"
                ) from error
            self.data = _load(self.run_id)
        except BaseException:
            # `__exit__` never runs for an `__enter__` that raises, so both locks go back here or a
            # single refusal wedges every later write.
            self._release()
            raise
        return self.data

    def _release(self) -> None:
        """Give back both locks, whatever happened, and never speak over the original failure.

        The outer release sits in a `finally` because a cleanup that raises must not cost the lock
        it was cleaning up after. Inner cleanup failures are swallowed for the same reason: a
        failure to unlock something is never more informative than whatever is already being
        raised, and a held state lock is worse than a lost traceback.
        """
        try:
            if self._run_document is not None:
                try:
                    self._run_document.__exit__(None, None, None)
                except BaseException:
                    pass
                self._run_document = None
        finally:
            handle, self._handle = self._handle, None
            if handle is not None:
                try:
                    _lease.fcntl.flock(handle.fileno(), _lease.fcntl.LOCK_UN)
                except BaseException:
                    pass
                finally:
                    handle.close()

    def __exit__(self, exc_type, *rest):
        try:
            if exc_type is None:
                # Committed on a clean exit rather than by an explicit call, so that a body which
                # forgets to commit cannot look like a successful write. The first draft of this
                # class made exactly that mistake.
                self.data["revision"] = self.data.get("revision", 0) + 1
                _save(self.run_id, self.data)
        finally:
            self._release()
        return False


def _load(run_id: str) -> dict[str, Any]:
    path = _path(run_id)
    try:
        raw = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return {"schema_version": SCHEMA_VERSION, "run_id": run_id,
                "sections": {name: [] for name in SECTIONS}}
    except OSError as error:
        raise StateError(f"cannot read {path}: {error}") from error
    try:
        value = json.loads(raw)
    except json.JSONDecodeError as error:
        raise StateError(f"{path.name} is corrupt; a damaged state is not an empty one") from error
    for name in SECTIONS:
        value.setdefault("sections", {}).setdefault(name, [])
    return value


def _save(run_id: str, value: dict[str, Any]) -> None:
    path = _path(run_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    os.chmod(path.parent, 0o700)
    _lease._write_json_atomically(path, value)


def _validated(section: str, text: str, boundary: str, allowed: tuple[str, ...]) -> dict[str, Any]:
    """Every refusal, before anything is opened, locked, or changed."""
    if section not in allowed:
        if section in SECTIONS:
            raise StateError(
                f"{section!r} accumulates; use record() so nothing is silently dropped")
        raise StateError(f"unknown section {section!r}; the sections are {list(SECTIONS)}")
    if boundary not in BOUNDARIES:
        raise StateError(
            f"unknown boundary {boundary!r}; state is updated at {list(BOUNDARIES)} and not "
            "whenever something happens, or it becomes the transcript it exists instead of"
        )
    if not text.strip():
        raise StateError("an empty entry records nothing")
    return {
        "text": text.strip(),
        "boundary": boundary,
        "at": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
    }


def record(run_id: str, token: str, section: str, text: str, *, boundary: str) -> None:
    """Add one entry at a named boundary, as the coordinator that owns the run.

    The boundary is required and checked. It is the whole mechanism keeping this a summary: an entry
    that cannot name the moment that made it worth writing is an entry that did not need writing.
    """
    entry = _validated(section, text, boundary, SECTIONS)
    with _Transaction(run_id, token) as value:
        value["sections"][section].append(entry)


def set_single(run_id: str, token: str, section: str, text: str, *, boundary: str) -> None:
    """Replace a section that holds one current answer rather than a growing list.

    One transaction, which it was not. The first version cleared the section and saved, then loaded
    and appended and saved again, so a crash between the two left the section empty: replacing a
    plan could cost you the plan. Atomic replacement of each individual file is not atomicity of
    this operation, and only one of those was ever true here.
    """
    entry = _validated(section, text, boundary, ("goal", "phase", "plan"))
    with _Transaction(run_id, token) as value:
        value["sections"][section] = [entry]


def render(run_id: str) -> str:
    """The state as an Actor receives it. Markdown, compact, no timestamps in the body."""
    value = _load(run_id)
    headings = {
        "goal": "Goal", "phase": "Current phase", "plan": "Current plan",
        "decisions": "Decisions made", "discoveries": "Important discoveries",
        "constraints": "Active constraints", "rejected": "Rejected alternatives",
        "open_questions": "Open questions",
    }
    lines = [f"# Shared task state: {run_id}", ""]
    for name in SECTIONS:
        entries = value["sections"][name]
        if not entries:
            continue
        lines.append(f"## {headings[name]}")
        lines.append("")
        for entry in entries:
            lines.append(f"- {entry['text']}")
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def briefing(run_id: str, *, unit_goal: str, files: list[str]) -> str:
    """Everything a joining Actor gets: the state, its own unit, and named files.

    Deliberately the whole interface. There is no parameter for a conversation, a transcript, or
    another Actor's context, because an Actor that needs those to act has not been given a work
    unit; it has been given somebody else's job.
    """
    if not unit_goal.strip():
        raise StateError("a work unit needs a goal an Actor can act on")
    parts = [render(run_id), "", "## Your work unit", "", unit_goal.strip(), ""]
    if files:
        parts += ["## Files named for this unit", ""]
        parts += [f"- `{name}`" for name in files]
    return "\n".join(parts).rstrip() + "\n"


def _strip_acting(text: str) -> str:
    """Remove anything that could act if this text were read on another machine."""
    for field in ACTING_FIELDS:
        text = re.sub(rf'"?{field}"?\s*[:=]\s*"?[A-Za-z0-9_\-]+"?', f"{field}: <not carried>", text)
    return text


def distil(run_id: str, *, title: str, active_topic: str, queued: list[str],
           unresolved: list[str], continues: str | None = None) -> str:
    """The durable record, in the shape `AB-SESSION-001` already defines.

    Unresolved work is carried rather than hidden: a handoff whose reader cannot see what nobody
    observed is worse than no handoff, because it reads as completion.
    """
    today = dt.datetime.now().strftime("%Y-%m-%d")
    lines = [f"# {title}", ""]
    if continues:
        lines += [f"**Continues**: `{continues}`", ""]
    lines += [f"**Date**: {today}", f"**Staffing run**: `{run_id}`", ""]
    lines += ["## Active topic", "", active_topic.strip(), ""]
    lines += ["## Queued topics", ""]
    lines += [f"- {item}" for item in queued] if queued else ["None."]
    lines += [""]
    if unresolved:
        lines += ["## Unresolved invocations", "",
                  "Effects nobody observed. Reconcile these before the work is treated as finished.",
                  ""]
        lines += [f"- `{item}`" for item in unresolved]
        lines += [""]
    body = render(run_id).split("\n", 1)[1].strip()
    lines += ["## What the run established", "", body, ""]
    return _strip_acting("\n".join(lines)).rstrip() + "\n"


def retire(run_id: str) -> None:
    """Drop live state. Call this only after `distil` has produced the durable record."""
    _path(run_id).unlink(missing_ok=True)
