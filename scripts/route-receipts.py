#!/usr/bin/env python3
"""Record content-free native helper lifecycle receipts in private local state."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import sys


SCHEMA_VERSION = 1
LOW_HELPER = "ab-low-helper"

# Staffing lifecycle events (roadmap item 148). Actor lifetime and invocation lifetime are separate
# kinds because they are separate facts: an Actor can outlive many invocations, and an invocation
# that never stopped is exactly the signal a crash leaves. A stop is therefore never manufactured to
# make counts pair; an unmatched start is reported as unmatched, which is the whole point of keeping
# it.
STAFFING_EVENTS = (
    "actor_start", "actor_stop",
    "invocation_start", "invocation_stop",
    "escalation", "lease_acquire", "lease_release",
)

# The only fields a staffing receipt may carry. Everything about the work itself is absent by
# construction rather than by reviewer discipline: no task, prompt, path, diff, transcript, or
# vendor event stream, which can carry source and tool output verbatim.
STAFFING_FIELDS = (
    "vendor", "role", "capability", "outcome", "attempt", "turns",
    # What was chosen, so an observed cost can be attributed to the choice that caused it. Neither
    # describes the work, which is what the content-free rule protects: `vendor` has always been
    # recorded in the clear for the same reason.
    "model", "effort",
    # Token categories, each priced differently, and never summed into one another. `total_input`
    # is everything the model read; `uncached_input` the part billed at the ordinary rate; a cache
    # read is a fraction of that rate and a cache write usually more than it. `thinking_tokens` is
    # part of `output_tokens` rather than additional to it.
    "total_input_tokens", "uncached_input_tokens", "cache_read_tokens", "cache_write_tokens",
    "output_tokens", "thinking_tokens",
    # Which vendor shape these numbers were read from. Receipts written before this schema existed
    # carry no such marker, and a reader that calibrates costs must skip them rather than assume
    # they meant the same thing.
    "usage_schema",
)


def state_dir() -> Path:
    override = os.environ.get("AB_ROUTE_RECEIPT_HOME")
    if override:
        return Path(override).expanduser()
    root = Path(os.environ.get("XDG_STATE_HOME", Path.home() / ".local" / "state"))
    return root / "agent-base" / "route-receipts"


def digest(value: object) -> str:
    return hashlib.sha256(str(value or "unknown").encode("utf-8")).hexdigest()[:24]


def events_path() -> Path:
    return state_dir() / "events.jsonl"


def record_lifecycle(payload: dict, vendor: str) -> int:
    event = payload.get("hook_event_name")
    if event not in {"SubagentStart", "SubagentStop"}:
        raise ValueError("expected SubagentStart or SubagentStop")
    if payload.get("agent_type") != LOW_HELPER:
        return 0
    directory = state_dir()
    directory.mkdir(parents=True, exist_ok=True)
    os.chmod(directory, 0o700)
    receipt = {
        "schema_version": SCHEMA_VERSION,
        "timestamp": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "vendor": vendor,
        "helper": LOW_HELPER,
        "event": "start" if event == "SubagentStart" else "stop",
        "session": digest(payload.get("session_id")),
        "agent": digest(payload.get("agent_id")),
    }
    target = events_path()
    with target.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(receipt, sort_keys=True) + "\n")
    os.chmod(target, 0o600)
    return 0


def record_claude(payload: dict) -> int:
    return record_lifecycle(payload, "claude")


def record_codex(payload: dict) -> int:
    return record_lifecycle(payload, "codex")


def record_staffing(event: str, *, run: str, actor: str, invocation: str | None = None,
                    model: str | None = None, **fields) -> int:
    """Append one content-free staffing receipt.

    Identifiers are one-way digests, so a later reader can pair a start with its stop and count
    attempts per Actor without learning which repository, run, or task any of it was.
    """
    if event not in STAFFING_EVENTS:
        raise ValueError(f"unknown staffing event {event!r}; known events are {STAFFING_EVENTS}")
    unknown = set(fields) - set(STAFFING_FIELDS)
    if unknown:
        raise ValueError(
            f"staffing receipts carry only {STAFFING_FIELDS}; refusing {sorted(unknown)} rather "
            "than recording something that might describe the work"
        )
    directory = state_dir()
    directory.mkdir(parents=True, exist_ok=True)
    os.chmod(directory, 0o700)
    receipt = {
        "schema_version": SCHEMA_VERSION,
        "timestamp": dt.datetime.now(dt.timezone.utc).isoformat().replace("+00:00", "Z"),
        "event": event,
        "run": digest(run),
        "actor": digest(actor),
        "invocation": digest(invocation) if invocation else None,
        # The model is recorded in the clear, unlike every identifier above it. An earlier version
        # digested it on the reasoning that an aggregate count does not need the name; that reason
        # stopped being true the moment these receipts became the evidence for what a model and an
        # effort cost, which is a question that cannot be answered about a hash. The content-free
        # rule protects the *work* - task, prompt, paths, transcript - and a model name describes
        # the tool rather than the work, alongside `vendor`, which has always been in the clear.
        "model": model or None,
        **{key: value for key, value in fields.items() if value is not None},
    }
    target = events_path()
    with target.open("a", encoding="utf-8") as handle:
        handle.write(json.dumps(receipt, sort_keys=True) + "\n")
    os.chmod(target, 0o600)
    return 0


def staffing_summary() -> dict:
    """Aggregate counts, including invocations that started and never stopped."""
    counts = {event: 0 for event in STAFFING_EVENTS}
    started: set[str] = set()
    stopped: set[str] = set()
    turns = 0
    try:
        lines = events_path().read_text(encoding="utf-8").splitlines()
    except FileNotFoundError:
        lines = []
    for line in lines:
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        kind = event.get("event")
        if kind not in counts:
            continue
        counts[kind] += 1
        if kind == "invocation_start" and event.get("invocation"):
            started.add(event["invocation"])
        if kind == "invocation_stop" and event.get("invocation"):
            stopped.add(event["invocation"])
        turns += int(event.get("turns") or 0)
    return {"counts": counts, "unmatched_invocations": sorted(started - stopped),
            "observed_turns": turns}


def status() -> int:
    counts = {vendor: {"start": 0, "stop": 0} for vendor in ("claude", "codex")}
    try:
        lines = events_path().read_text(encoding="utf-8").splitlines()
    except FileNotFoundError:
        lines = []
    for line in lines:
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        vendor = event.get("vendor")
        if vendor in counts and event.get("helper") == LOW_HELPER:
            if event.get("event") in counts[vendor]:
                counts[vendor][event["event"]] += 1
    for vendor, label in (("claude", "Claude"), ("codex", "Codex")):
        print(f"{label} {LOW_HELPER} receipts: start={counts[vendor]['start']} stop={counts[vendor]['stop']}")
    summary = staffing_summary()
    if any(summary["counts"].values()):
        pairs = " ".join(f"{k}={v}" for k, v in summary["counts"].items() if v)
        print(f"Staffing receipts: {pairs}")
        print(f"Observed turns: {summary['observed_turns']}")
        if summary["unmatched_invocations"]:
            # Said plainly rather than smoothed over: an invocation with no stop is a crash or a
            # kill, and a count that looked balanced would hide exactly the thing worth seeing.
            print(f"Invocations that never stopped: {len(summary['unmatched_invocations'])}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("claude-hook", "codex-hook", "status"))
    args = parser.parse_args(argv)
    try:
        if args.command == "status":
            return status()
        payload = json.load(sys.stdin)
        if args.command == "claude-hook":
            return record_claude(payload)
        result = record_codex(payload)
        if payload.get("hook_event_name") == "SubagentStop":
            print("{}")
        return result
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        print(f"Route receipt error: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
