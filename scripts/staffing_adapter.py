#!/usr/bin/env python3
"""Turning an invocation contract into a vendor command line (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    A contract's write scope decides its arguments. Read-only never produces a command that could
    write, and an unverified resume path never produces a resume.

This is the single place a vendor CLI contract is written down for staffing. Three separate
implementations of "how to launch Claude or Codex" already exist in this repository, in the two
fleet launch gates and in the Executor/Reviewer loop, and that duplication is how one of them could
drift into launching with `--dangerously-bypass-approvals-and-sandbox` for two days without the
other two noticing. Migrating those three onto this module is deliberately a later, separately
tested step: introducing one new consumer is a smaller risk than re-plumbing three verified
mechanisms in the same change.

The module builds argument lists and an environment overlay. It launches nothing. Resolution and
execution are kept apart so every decision here is testable without spending a model.
"""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import sys
from typing import NamedTuple


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


_contract = _sibling("staffing_contract")
_invariants = _sibling("vendor_launch")

CLAUDE = "claude"
CODEX = "codex"
SUPPORTED_VENDORS = (CLAUDE, CODEX)


class AdapterError(RuntimeError):
    """Every refusal here. All of them fail closed."""


class Launch(NamedTuple):
    argv: list[str]
    environment: dict[str, str]
    cwd: str
    timeout_seconds: int
    # The *most* this route could ever establish about writer completion, decided before anything
    # runs. A ceiling, not evidence: it says what kind of promise is available on this route, never
    # that this invocation kept it. Those were read as the same thing, so a CLI that returned zero
    # was credited with a cooperative completion it had never been asked for and never given.
    # What this invocation actually acknowledged arrives in its result envelope.
    completion_ceiling: str


# A foreground CLI that owns its tools can be waited for; what its permitted tools do afterwards is
# not observable from outside. Neither vendor currently gives agent-base a mechanically observed
# completion, so both declare the weaker level honestly.
COOPERATIVE = "cooperative"


def _claude_argv(contract, resume_handle: str | None) -> list[str]:
    argv = [CLAUDE, "--print", "--model", contract.model]
    if contract.effort:
        argv += ["--effort", contract.effort]
    if resume_handle:
        argv += ["--resume", resume_handle]
    else:
        argv += ["--session-id", contract.invocation_id]
    if contract.max_turns is not None:
        argv += ["--max-turns", str(contract.max_turns)]
    argv += ["--output-format", "json"]
    if contract.write_scope == _contract.WRITE_WORKTREE:
        # `acceptEdits`, not `auto`. The fleet launcher uses `auto` because it runs against a
        # trusted client root with machine-wide Auto mode configured, where a boundary crossing
        # reaches a reviewer. A staffing writer is a non-interactive process with nobody to answer:
        # under `auto` it stops and asks, the prompt reaches no one, and the invocation returns
        # having written nothing while looking like a completed run. Observed live on 2026-09-18.
        # `acceptEdits` accepts edits inside the working directory and still stops for anything
        # beyond it, which is the narrower behaviour a bounded writer actually wants.
        argv += ["--permission-mode", "acceptEdits"]
    else:
        # A restricted permission mode, a built-in tool allowlist, and no external tool servers.
        # The third is defence in depth rather than a repair. Measured 2026-09-18 by attempting the
        # call: with the allowlist alone an external tool was offered and its use was *denied* by
        # `dontAsk`, so the boundary already held. Excluding the servers means an Actor cannot
        # attempt what it has no business attempting, and the permission denial stops being the only
        # thing between an advisory Actor and the world.
        argv += ["--permission-mode", "dontAsk", "--tools", "Read,Glob,Grep",
                 "--strict-mcp-config"]
    return argv


def _codex_argv(contract, resume_handle: str | None) -> list[str]:
    argv = [CODEX, "exec"]
    if resume_handle:
        # `codex exec resume` does not accept `-C` or `-s` on the subcommand, which is why resume
        # requires a verified restatement path before it may be used at all. Parent-level placement
        # parses, but parsing is not evidence that the restriction applies to the resumed session.
        argv += ["resume", resume_handle]
    argv += ["-m", contract.model]
    if contract.effort:
        argv += ["-c", f'model_reasoning_effort="{contract.effort}"']
    if not resume_handle:
        argv += ["-C", contract.worktree, "--add-dir", contract.worktree]
        if contract.write_scope == _contract.WRITE_WORKTREE:
            # `--approve-for-me` alone: it already means "route approvals through automatic review
            # using the workspace-write sandbox", and this Codex refuses the pair outright with
            # "the argument '--sandbox <SANDBOX_MODE>' cannot be used with '--approve-for-me'".
            # Naming the sandbox as well looked like belt and braces and was a launch that could not
            # start. The fleet launcher never paired them; this adapter had. Observed live
            # 2026-09-18.
            argv += ["--approve-for-me"]
        else:
            # `--ignore-user-config` for the same reason as Claude's `--strict-mcp-config`, and
            # with the same honest scope. Measured 2026-09-18 by attempting the write: a read-only
            # Codex was offered `apply_patch` and the sandbox blocked it, so this narrows the
            # inventory rather than closing a hole. Model and effort are passed explicitly here, so
            # nothing this launcher relies on came from that config.
            argv += ["-s", "read-only", "-c", 'approval_policy="never"', "--ignore-user-config"]
    argv += ["--json"]
    return argv


def build_launch(contract, *, resume_handle: str | None = None,
                 resume_verified: bool = False) -> Launch:
    """Assemble one invocation, refusing anything the contract does not actually permit."""
    if contract.vendor not in SUPPORTED_VENDORS:
        raise AdapterError(
            f"vendor {contract.vendor!r} has no verified launch contract; supported vendors are "
            f"{list(SUPPORTED_VENDORS)}. Research and record one before staffing it."
        )
    if resume_handle and contract.vendor == CODEX:
        # Read from this operator's installed `codex exec resume --help`, which lists neither `-C`
        # nor `-s`, so the command agent-base would build cannot state the root it is confined to or
        # the sandbox it runs in. That is what was checked; whether some other invocation or version
        # could express it was not. A fresh restricted session costs a session and needs no claim
        # about a vendor's whole surface, so the refusal stands and the reason is narrowed.
        raise AdapterError(
            "the codex resume command agent-base builds cannot restate an invocation contract: its "
            "subcommand takes neither a working root nor a sandbox mode, so a fresh restricted "
            "session is created instead"
        )
    if resume_handle and not resume_verified:
        raise AdapterError(
            f"{contract.vendor}'s resume path has not been demonstrated to restate an invocation "
            "contract for this operator; create a fresh restricted session instead"
        )
    if contract.write_scope == _contract.WRITE_WORKTREE and not contract.lease_token:
        raise AdapterError("a writing invocation reached the adapter without the worktree lease")

    argv = (_claude_argv if contract.vendor == CLAUDE else _codex_argv)(contract, resume_handle)
    # Submitted to the shared checker before it can be handed back. Every launcher in this
    # repository does this, so a fact one of them learns cannot stay local to it.
    _invariants.check(contract.vendor, argv,
                      may_write=contract.write_scope == _contract.WRITE_WORKTREE)
    return Launch(
        argv=argv,
        # Replaced, never merged: a child must not inherit the coordinator's context, and the
        # cheapest way to guarantee that is for the launcher to write the value itself every time.
        environment=_contract.to_environment(contract),
        cwd=contract.worktree,
        timeout_seconds=contract.wall_clock_seconds,
        completion_ceiling=COOPERATIVE,
    )


def normalize_output(vendor: str, stdout: str) -> str:
    """Reduce a vendor's own framing to the Actor's own words, so one parser can read both.

    Each vendor wraps a result in its own envelope, and neither wrapper is the result. Claude's
    `--output-format json` returns one object with the final text under `result`; Codex's `--json`
    returns a JSONL event stream whose agent messages carry it under `item.text`. Left untranslated,
    a result parser sees Codex's event objects, finds no envelope of ours at their top level, and
    reports a perfectly good run as having produced nothing. Observed while confirming vendor
    neutrality on 2026-09-18.

    Anything unrecognized is returned unchanged rather than discarded: a parser refusing an odd
    string is a better failure than this function silently deciding there was no output.
    """
    if vendor == CLAUDE:
        try:
            value = json.loads(stdout)
        except (ValueError, TypeError):
            return stdout
        return str(value.get("result", stdout)) if isinstance(value, dict) else stdout
    if vendor == CODEX:
        messages = []
        for line in stdout.splitlines():
            line = line.strip()
            if not line.startswith("{"):
                continue
            try:
                event = json.loads(line)
            except ValueError:
                continue
            item = event.get("item") or {}
            if item.get("type") == "agent_message" and isinstance(item.get("text"), str):
                messages.append(item["text"])
        return "\n".join(messages) if messages else stdout
    return stdout


class Usage(NamedTuple):
    """What an invocation spent, in categories that are priced differently. None means unknown.

    One schema for both vendors, because the earlier three-field tuple meant different things on
    each and a consumer following its declared meaning would have double-counted. Two live schema
    mismatches motivated every field here.

    **Total and uncached input are separate.** One vendor reports `input_tokens` already excluding
    cached context; the other reports it including cached context. Calling either one "fresh" made
    the same name mean two things. `total_input` is everything the model read; `uncached_input` is
    the part billed at the ordinary rate.

    **Cache reads and cache writes are separate.** They are priced differently and in opposite
    directions: a read is a fraction of the ordinary rate, a write is usually more than it. Summing
    them made 100 read plus 900 written indistinguishable from 900 read plus 100 written, which are
    not interchangeable costs, and made the convenient phrase "cached input is roughly a tenth"
    false for half of what it described.

    **`thinking` is part of `output`, not additional to it.** It is the field that moves with
    reasoning effort; a total can stay flat while thinking grows and the answer shortens. It detects
    the effect. It cannot price it, because what is billed is the total.

    `schema` records which vendor's shape these numbers were read from, so receipts written before
    this schema existed cannot silently become calibrated data.
    """

    total_input: int | None = None
    uncached_input: int | None = None
    cache_read: int | None = None
    cache_write: int | None = None
    output: int | None = None
    thinking: int | None = None
    schema: str | None = None


USAGE_SCHEMA = "ab-usage-v2"


def usage(vendor: str, stdout: str) -> Usage:
    """Read what this invocation spent out of the vendor's own output.

    Nothing is inferred that the vendor did not report. Where a field is absent it stays None rather
    than becoming a zero or a subtraction from numbers that do not mean what the subtraction would
    assume; an invented split is worse than an admitted gap, because it looks like data.
    """
    if vendor == CLAUDE:
        try:
            value = json.loads(stdout)
        except (ValueError, TypeError):
            return Usage()
        counts = value.get("usage") if isinstance(value, dict) else None
        if not isinstance(counts, dict):
            return Usage()
        # Measured against this vendor's own output on 2026-09-19: `input_tokens` was 10 while
        # `cache_creation_input_tokens` was 8,004 for a prompt of about 8,014 tokens. So here
        # `input_tokens` is the uncached part and the total is the sum.
        uncached = _count(counts.get("input_tokens"))
        read = _count(counts.get("cache_read_input_tokens"))
        write = _count(counts.get("cache_creation_input_tokens"))
        # The total is the sum of the three, so it is knowable only when all three are reported.
        # Summing whichever happened to be present published a subtotal under the name of a total:
        # an output carrying `input_tokens` alone yielded a "total" equal to the uncached part, with
        # both cache categories unknown beside it. A known subtotal is not a known total.
        parts = (uncached, read, write)
        details = counts.get("output_tokens_details")
        return Usage(
            total_input=sum(parts) if all(part is not None for part in parts) else None,
            uncached_input=uncached, cache_read=read, cache_write=write,
            output=_count(counts.get("output_tokens")),
            thinking=_count(details.get("thinking_tokens")) if isinstance(details, dict) else None,
            schema=f"{USAGE_SCHEMA}/{CLAUDE}")
    if vendor == CODEX:
        latest = Usage()
        for line in stdout.splitlines():
            line = line.strip()
            if not line.startswith("{"):
                continue
            try:
                event = json.loads(line)
            except ValueError:
                continue
            if not isinstance(event, dict):
                continue
            for holder in (event, event.get("msg") or {}, event.get("info") or {}):
                if not isinstance(holder, dict):
                    continue
                counts = holder.get("usage") if isinstance(holder.get("usage"), dict) else holder
                total = _count(counts.get("input_tokens"))
                cached = _count(counts.get("cached_input_tokens"))
                output = _count(counts.get("output_tokens"))
                if total is None and output is None:
                    continue
                # This vendor's `input_tokens` includes the cached part, so the uncached remainder
                # is a subtraction rather than the field itself.
                #
                # A cached count larger than the total contradicts that convention, and clamping it
                # to zero turned the contradiction into an apparently free invocation. Two
                # observations that cannot both be true do not average into a cheap one; the
                # remainder is simply unknown, and the inconsistency is preserved in the components
                # so that whoever reads it can see what disagreed.
                uncached = None
                if total is not None and cached is not None and cached <= total:
                    uncached = total - cached
                latest = Usage(
                    total_input=total, uncached_input=uncached, cache_read=cached,
                    # No cache-write figure is reported here; unknown stays unknown.
                    cache_write=None, output=output,
                    thinking=_count(counts.get("reasoning_output_tokens")),
                    schema=f"{USAGE_SCHEMA}/{CODEX}")
        return latest
    return Usage()


def _count(value: object) -> int | None:
    return int(value) if isinstance(value, (int, float)) and not isinstance(value, bool) else None


def provider_handle(vendor: str, stdout: str) -> str | None:
    """The vendor's own identifier for this conversation, or None when it did not give one.

    Read from the vendor's output rather than assumed from what was sent. The runtime used to record
    its own `invocation_id` as the provider handle for every vendor, which is a fabricated recovery
    handle: it is exactly the value agent-base already knew, wearing the name of something it had
    learned. For Claude that value happens to be supplied on the command line, so recording it looked
    right; for Codex nothing supplies it at all, and a Codex run recorded a local identifier as
    though a provider had returned it.

    Absence stays absence. An unknown handle is a true statement about what is known, while a
    plausible-looking one sends whoever recovers this run to a conversation that does not exist.
    """
    if vendor == CLAUDE:
        try:
            value = json.loads(stdout)
        except (ValueError, TypeError):
            return None
        handle = value.get("session_id") if isinstance(value, dict) else None
        return handle if isinstance(handle, str) and handle else None
    if vendor == CODEX:
        for line in stdout.splitlines():
            line = line.strip()
            if not line.startswith("{"):
                continue
            try:
                event = json.loads(line)
            except ValueError:
                continue
            if not isinstance(event, dict):
                continue
            for holder in (event, event.get("msg") or {}, event.get("item") or {}):
                if not isinstance(holder, dict):
                    continue
                for field in ("session_id", "thread_id", "conversation_id"):
                    handle = holder.get(field)
                    if isinstance(handle, str) and handle:
                        return handle
    return None


def writes(launch: Launch) -> bool:
    """Whether these arguments could mutate the worktree, judged from the arguments themselves.

    A second, independent reading of the same question the contract already answered. It exists so a
    test can catch an adapter that says read-only and builds a writable command, which is the one
    mistake the contract's own validation cannot see.
    """
    return _invariants.writes(launch.argv[0], launch.argv)
