#!/usr/bin/env python3
"""One writer at a time per working tree, for agent-base Venture Staffing (roadmap item 148).

The predicate this module exists to enforce, stated before any code reads it:

    No writer may be launched unless the runtime has established, inside a serialized critical
    section, that the worktree has no current owner and no unresolved uncertain writer.

Everything below is that sentence made executable. Three properties carry it:

1. **Transitions are serialized, not merely checked.** Comparing an ownership token and then
   unlinking a path is not an atomic compare-and-delete: an owner can read its token, be overtaken
   by a recovery and a new acquisition, and then unlink its successor's lease. Acquire, release and
   reclaim therefore run inside a stable `fcntl.flock` on a control file that is never unlinked,
   and ownership is rechecked inside that lock. The pattern is `human-interruptions.py`'s, reused
   rather than reinvented.

2. **Uncertainty outlives the process that discovered it.** There is no cheap universal signal that
   an opaque CLI's descendants have stopped writing, so when completion cannot be established the
   runtime records an uncertain writer and refuses every later writer for that worktree. That record
   is a separate durable file precisely so that releasing the control lock on a crash cannot erase
   it. Clearing it requires recorded operator evidence; a confirmation with no evidence would be an
   override of the guarantee rather than an implementation of it.

3. **The boundary is declared, not implied.** This coordinates participating agent-base executions
   on a POSIX filesystem with working `fcntl` locking. It does not coordinate a human's editor, an
   unrelated process, or a detached descendant of a model's own tool, and it never silently
   downgrades to a process-local lock when the platform cannot support the real one.
"""

from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import errno
import hashlib
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
from typing import NamedTuple

try:
    import fcntl
except ImportError:  # pragma: no cover - exercised only on a platform without it
    fcntl = None  # type: ignore[assignment]


SCHEMA_VERSION = 1

# How a release established that writing had stopped. Recorded rather than assumed, because the two
# levels support different claims and conflating them is how a cooperative promise gets reported as
# an observation (Astra, 260918_1756).
COMPLETION_OBSERVED = "mechanically_observed"
COMPLETION_COOPERATIVE = "cooperative"
COMPLETION_LEVELS = (COMPLETION_OBSERVED, COMPLETION_COOPERATIVE)


class LeaseError(RuntimeError):
    """Any refusal to grant, release, or clear. Every one of these fails closed."""


class UnsupportedPlatform(LeaseError):
    """Raised instead of downgrading to a lock that does not actually exclude anything."""


class Lease(NamedTuple):
    """A NamedTuple rather than a dataclass on purpose: every consumer in this repository loads
    modules by file path through importlib, and a dataclass resolves its own module out of
    `sys.modules` at class-creation time, which is not populated on that path."""

    worktree: str
    run_id: str
    actor_id: str
    invocation_id: str
    token: str
    generation: int
    acquired_at: str

    def as_dict(self) -> dict:
        return {"schema_version": SCHEMA_VERSION, **self._asdict()}


def state_dir() -> Path:
    """Where leases live. `AB_STAFFING_STATE_HOME` moves the whole tree, including run records.

    Worth knowing before it is needed: the default sits under `XDG_STATE_HOME`, and a machine whose
    `~/.local/state` is owned by root cannot create anything there. That is not a hypothetical; it
    stopped this feature's first real task on a real client before any model was reached.
    """
    override = os.environ.get("AB_STAFFING_STATE_HOME")
    if override:
        return Path(override).expanduser()
    root = Path(os.environ.get("XDG_STATE_HOME", Path.home() / ".local" / "state"))
    return root / "agent-base" / "staffing" / "leases"


def canonical_worktree(path: Path) -> Path:
    """The repository's own idea of its root, resolved through symlinks.

    Two aliases of one checkout must collide, and two genuinely distinct worktrees must not. Asking
    Git first is what makes a subdirectory and its root produce the same key; resolving afterwards
    is what makes `/tmp` and `/private/tmp` produce the same key on macOS.
    """
    try:
        top = subprocess.run(
            ["git", "-C", str(path), "rev-parse", "--show-toplevel"],
            capture_output=True, text=True, check=False,
        )
    except OSError as error:
        raise LeaseError(f"cannot run git to identify the worktree: {error}") from error
    if top.returncode or not top.stdout.strip():
        raise LeaseError(f"not inside a Git worktree: {path}")
    return Path(top.stdout.strip()).resolve()


def worktree_key(worktree: Path) -> str:
    return hashlib.sha256(str(worktree).encode("utf-8")).hexdigest()[:24]


def _ensure_private_dir(path: Path) -> None:
    try:
        path.mkdir(parents=True, exist_ok=True)
        os.chmod(path, 0o700)
    except OSError as error:
        # A state directory that cannot be created means no lease can be recorded, and a lease that
        # is not recorded excludes nobody. Say that plainly instead of surfacing a traceback from
        # three frames inside pathlib.
        raise LeaseError(
            f"cannot prepare the staffing state directory {path}: {error}. Set "
            "AB_STAFFING_STATE_HOME to a writable location, or grant access to this one."
        ) from error


def _paths(worktree: Path) -> tuple[Path, Path, Path]:
    """Control lock, lease, uncertain record. The lock is never unlinked; the other two come and go."""
    directory = state_dir()
    key = worktree_key(worktree)
    return directory / f"{key}.lock", directory / f"{key}.json", directory / f"{key}.uncertain.json"


@contextlib.contextmanager
def _critical_section(worktree: Path):
    """Serialize one metadata transition. Short by construction: never hold this across a human."""
    if fcntl is None:
        raise UnsupportedPlatform(
            "fcntl locking is unavailable on this platform; agent-base will not substitute a "
            "process-local lock, which would exclude nothing between processes"
        )
    lock_path, _, _ = _paths(worktree)
    _ensure_private_dir(lock_path.parent)
    # Opened "a+" and never truncated or unlinked: the inode has to stay stable for the lock to
    # mean anything, and it is also the only file guaranteed to exist, which makes it the right
    # place to keep a generation counter that must not reset when the lease JSON is deleted.
    with lock_path.open("a+", encoding="utf-8") as handle:
        os.chmod(lock_path, 0o600)
        try:
            fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
        except OSError as error:
            if error.errno in (errno.ENOLCK, errno.EOPNOTSUPP, errno.EINVAL):
                raise UnsupportedPlatform(
                    f"this filesystem does not support the required lock: {error}"
                ) from error
            raise
        try:
            yield handle
        finally:
            fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def _read_generation(handle) -> int:
    handle.seek(0)
    text = handle.read().strip()
    if not text:
        return 0
    try:
        return int(json.loads(text)["generation"])
    except (ValueError, KeyError, TypeError):
        # An unreadable counter is not a zero counter. Refusing is the only safe reading, because
        # restarting at zero is exactly how a stale release could match a new lease.
        raise LeaseError("control record is unreadable; resolve it before staffing this worktree")


def _write_generation(handle, generation: int) -> None:
    handle.seek(0)
    handle.truncate()
    handle.write(json.dumps({"schema_version": SCHEMA_VERSION, "generation": generation}) + "\n")
    handle.flush()
    os.fsync(handle.fileno())


def _read_json(path: Path) -> dict | None:
    """None means absent. A corrupt or partly written record raises instead: it is not absence."""
    try:
        text = path.read_text(encoding="utf-8")
    except FileNotFoundError:
        return None
    except OSError as error:
        raise LeaseError(f"cannot read {path.name}: {error}") from error
    try:
        value = json.loads(text)
    except json.JSONDecodeError as error:
        raise LeaseError(
            f"{path.name} is corrupt; a damaged record is not an absent one and this worktree "
            "stays closed to writers until it is resolved"
        ) from error
    if not isinstance(value, dict):
        raise LeaseError(f"{path.name} does not hold an object")
    return value


def _write_json_atomically(path: Path, payload: dict) -> None:
    temporary = path.with_suffix(path.suffix + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.chmod(temporary, 0o600)
    with temporary.open("r+", encoding="utf-8") as handle:
        os.fsync(handle.fileno())
    os.replace(temporary, path)


def _now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z")


def acquire(worktree: Path, run_id: str, actor_id: str, invocation_id: str) -> Lease:
    """Grant the right to write, or refuse. Never blocks on another run's work.

    The lease is persisted before this returns and therefore before any writer is launched. A crash
    between here and the writer's completion leaves an ownership record standing, which refuses the
    next writer, which is the safe direction: a stuck lease is visible, a lost one is not.
    """
    worktree = canonical_worktree(worktree)
    lock_path, lease_path, uncertain_path = _paths(worktree)
    with _critical_section(worktree) as handle:
        uncertain = _read_json(uncertain_path)
        if uncertain is not None:
            raise LeaseError(
                f"an unresolved writer from invocation {uncertain.get('invocation_id')} may still "
                f"be writing {worktree}; clear it with recorded evidence before staffing again"
            )
        current = _read_json(lease_path)
        if current is not None:
            raise LeaseError(
                f"{worktree} is held by invocation {current.get('invocation_id')} "
                f"(run {current.get('run_id')}, actor {current.get('actor_id')})"
            )
        generation = _read_generation(handle) + 1
        lease = Lease(
            worktree=str(worktree), run_id=run_id, actor_id=actor_id,
            invocation_id=invocation_id, token=secrets.token_hex(16),
            generation=generation, acquired_at=_now(),
        )
        # The counter advances in the durable control file first. If the lease write fails the
        # generation is merely skipped, which is harmless; the reverse order could hand a later
        # acquisition a number a stale release still matches.
        _write_generation(handle, generation)
        _write_json_atomically(lease_path, lease.as_dict())
        return lease


def release(worktree: Path, token: str, completion: str, detail: str = "") -> str:
    """Give the lease up. Returns the resulting state: "released" or "uncertain".

    `completion` must name how writing was established to have stopped. An unrecognized level is
    treated as no establishment at all, because a level nobody verified is not evidence.
    """
    worktree = canonical_worktree(worktree)
    _, lease_path, uncertain_path = _paths(worktree)
    with _critical_section(worktree):
        current = _read_json(lease_path)
        if current is None:
            raise LeaseError(f"{worktree} holds no lease to release")
        if current.get("token") != token:
            # The whole point of the critical section. A token read before someone else acquired
            # cannot authorize anything now, so a late release is refused rather than applied to a
            # successor's lease.
            raise LeaseError(
                "this token does not own the current lease; a later acquisition has replaced it"
            )
        if completion in COMPLETION_LEVELS:
            lease_path.unlink(missing_ok=True)
            return "released"
        # Uncertainty is persisted before ownership is dropped, so no moment exists in which
        # neither record stands while a previous writer's termination is unproven.
        _write_json_atomically(uncertain_path, {
            "schema_version": SCHEMA_VERSION,
            "worktree": str(worktree),
            "run_id": current.get("run_id"),
            "actor_id": current.get("actor_id"),
            "invocation_id": current.get("invocation_id"),
            "token": token,
            "generation": current.get("generation"),
            "recorded_at": _now(),
            # Both, because they answer different questions for whoever recovers this. `completion`
            # says what level was offered and rejected; `reason` says what was happening at the
            # time. A record holding only the second leaves the reader guessing whether anything
            # was claimed about writing at all.
            "completion": completion,
            "reason": detail or f"completion not established (given: {completion!r})",
        })
        lease_path.unlink(missing_ok=True)
        return "uncertain"


def clear_uncertain(worktree: Path, invocation_id: str, evidence: str) -> None:
    """Reopen a worktree after an operator actually stopped the writer and said how.

    Evidence is required and unchecked by anything but a human reading it later. That asymmetry is
    deliberate: agent-base can demand that an intervention be recorded, and cannot verify it, so it
    refuses to pretend that a bare confirmation is the same thing.
    """
    if not evidence.strip():
        raise LeaseError(
            "clearing an uncertain writer requires recorded evidence of what was stopped and how "
            "that was observed; a confirmation alone overrides the guarantee instead of meeting it"
        )
    worktree = canonical_worktree(worktree)
    _, _, uncertain_path = _paths(worktree)
    with _critical_section(worktree):
        # Rechecked inside the lock: the record may have been replaced while evidence was gathered.
        record = _read_json(uncertain_path)
        if record is None:
            raise LeaseError(f"{worktree} has no uncertain writer to clear")
        if record.get("invocation_id") != invocation_id:
            raise LeaseError(
                f"the uncertain writer is invocation {record.get('invocation_id')}, not "
                f"{invocation_id}; re-read the record before clearing it"
            )
        resolved = dict(record)
        resolved.update({"cleared_at": _now(), "evidence": evidence.strip()})
        _write_json_atomically(uncertain_path.with_suffix(".resolved.json"), resolved)
        uncertain_path.unlink(missing_ok=True)


def status(worktree: Path) -> dict:
    worktree = canonical_worktree(worktree)
    _, lease_path, uncertain_path = _paths(worktree)
    with _critical_section(worktree) as handle:
        uncertain = _read_json(uncertain_path)
        lease = _read_json(lease_path)
        return {
            "worktree": str(worktree),
            "generation": _read_generation(handle),
            "state": "uncertain" if uncertain else ("owned" if lease else "free"),
            "lease": lease,
            "uncertain": uncertain,
        }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Inspect the staffing write lease for a worktree.")
    parser.add_argument("command", choices=("status",))
    parser.add_argument("--root", type=Path, default=Path.cwd())
    args = parser.parse_args(argv)
    try:
        report = status(args.root)
    except LeaseError as error:
        print(f"Staffing lease: {error}", file=sys.stderr)
        return 1
    print(f"{report['worktree']}: {report['state']} (generation {report['generation']})")
    if report["uncertain"]:
        print(f"  unresolved writer: invocation {report['uncertain'].get('invocation_id')}")
        print(f"  reason: {report['uncertain'].get('reason')}")
    elif report["lease"]:
        print(f"  held by invocation {report['lease'].get('invocation_id')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
