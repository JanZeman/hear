#!/usr/bin/env python3
"""Measure agent-initiated human requests without storing conversation content.

The collector is deliberately local and review-only.  It records normalized event
metadata from Codex and Claude hooks, reports round trips, and suggests narrow
permission candidates.  It never approves a request or edits a permission rule.
"""

from __future__ import annotations

import argparse
import contextlib
import datetime as dt
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import sys
import tempfile
import uuid


SCHEMA_VERSION = 1
DEFAULT_RETENTION_DAYS = 30
DEFAULT_RECOMMENDATION_MINIMUM = 2
SUPPORTED_VENDORS = ("codex", "claude")
CATEGORIES = {
    "approval",
    "clarification",
    "policy_confirmation",
    "action_request",
    "handoff",
}
OUTCOMES = {
    "pending",
    "accepted",
    "declined",
    "aborted",
    "auto_resolved",
    "not_required",
    "unknown",
}
QUESTION_CATEGORIES = {"clarification", "policy_confirmation", "action_request"}
HOOK_EVENTS = {
    "codex": (
        "PermissionRequest",
        "PostToolUse",
        "UserPromptSubmit",
        "SubagentStop",
        "Stop",
        "SessionEnd",
    ),
    "claude": (
        "PermissionRequest",
        "PostToolUse",
        "PostToolUseFailure",
        "UserPromptSubmit",
        "SubagentStop",
        "Stop",
        "SessionEnd",
    ),
}
SENSITIVE = re.compile(
    r"(?i)(secret|token|password|passwd|api[_-]?key|authorization|bearer|credential)"
)
SHELL_META = re.compile(r"[;&|><$`(){}\n\r]")
POLICY_QUESTION = re.compile(
    r"(?i)\b(do you want|would you like|shall i|may i|should i|can i|"
    r"could i|confirm|permission|approve|proceed)\b"
)
ACTION_REQUEST = re.compile(
    r"(?i)\bplease\s+(run|review|commit|push|clean|choose|confirm|provide|reply|decide)\b"
)
HANDOFF = re.compile(
    r"(?is)(git\s+add\s+\.|git\s+commit|review.+commit|commit.+push|"
    r"if\s+you\s+disagree|if\s+anything\s+looks\s+wrong)"
)


def utcnow() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


def iso(value: dt.datetime | None = None) -> str:
    return (value or utcnow()).isoformat().replace("+00:00", "Z")


def parse_time(value: str) -> dt.datetime:
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))


def xdg_path(env_name: str, fallback: Path) -> Path:
    value = os.environ.get(env_name)
    return Path(value).expanduser() if value else fallback


def preferred_state_dir() -> Path:
    root = xdg_path("XDG_STATE_HOME", Path.home() / ".local" / "state")
    return root / "agent-base" / "human-interruptions"


def state_dir() -> Path:
    override = os.environ.get("AB_HUMAN_INTERRUPTION_HOME")
    if override:
        return Path(override).expanduser()
    path = config_path()
    if path.exists():
        try:
            configured = json.loads(path.read_text(encoding="utf-8")).get("state_dir")
        except (OSError, json.JSONDecodeError, AttributeError):
            configured = None
        if isinstance(configured, str) and configured:
            return Path(configured).expanduser()
    return preferred_state_dir()


def config_dir() -> Path:
    override = os.environ.get("AB_HUMAN_INTERRUPTION_CONFIG_HOME")
    if override:
        return Path(override).expanduser()
    root = xdg_path("XDG_CONFIG_HOME", Path.home() / ".config")
    return root / "agent-base" / "human-interruptions"


def data_dir() -> Path:
    override = os.environ.get("AB_HUMAN_INTERRUPTION_DATA_HOME")
    if override:
        return Path(override).expanduser()
    root = xdg_path("XDG_DATA_HOME", Path.home() / ".local" / "share")
    return root / "agent-base"


def config_path() -> Path:
    return config_dir() / "config.json"


def events_path() -> Path:
    return state_dir() / "events.jsonl"


def runtime_path() -> Path:
    return state_dir() / "runtime.json"


def installed_script() -> Path:
    return data_dir() / "human-interruptions.py"


def ensure_private_dir(path: Path) -> None:
    try:
        path.mkdir(parents=True, exist_ok=True, mode=0o700)
        path.chmod(0o700)
    except OSError as exc:
        raise RuntimeError(f"cannot prepare private directory {path}: {exc}") from exc


def path_can_be_created(path: Path) -> bool:
    ancestor = path
    while not ancestor.exists() and ancestor != ancestor.parent:
        ancestor = ancestor.parent
    return ancestor.is_dir() and os.access(ancestor, os.W_OK)


def install_state_dir(cfg: dict) -> Path:
    configured = cfg.get("state_dir")
    if isinstance(configured, str) and configured:
        return Path(configured).expanduser()
    preferred = preferred_state_dir()
    if path_can_be_created(preferred):
        return preferred
    return data_dir() / "human-interruptions-state"


def atomic_json(path: Path, data: object, mode: int = 0o600) -> None:
    ensure_private_dir(path.parent)
    fd, tmp_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            json.dump(data, handle, indent=2, sort_keys=True)
            handle.write("\n")
        os.chmod(tmp_name, mode)
        os.replace(tmp_name, path)
    finally:
        with contextlib.suppress(FileNotFoundError):
            os.unlink(tmp_name)


def default_config(enabled: bool = False) -> dict:
    return {
        "schema_version": SCHEMA_VERSION,
        "enabled": enabled,
        "vendors": [],
        "retention_days": DEFAULT_RETENTION_DAYS,
        "salt": uuid.uuid4().hex,
    }


def load_config(create: bool = False) -> dict:
    path = config_path()
    if not path.exists():
        cfg = default_config()
        if create:
            atomic_json(path, cfg)
        return cfg
    try:
        cfg = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"cannot read {path}: {exc}") from exc
    changed = False
    for key, value in default_config().items():
        if key not in cfg:
            cfg[key] = value
            changed = True
    if changed and create:
        atomic_json(path, cfg)
    if create:
        os.chmod(path, 0o600)
    return cfg


@contextlib.contextmanager
def state_lock(create: bool = True):
    root = state_dir()
    lock_path = root / ".lock"
    if not create and (not root.is_dir() or not lock_path.exists()):
        yield
        return
    if create:
        ensure_private_dir(root)
    mode = "a+" if create else "r"
    with lock_path.open(mode, encoding="utf-8") as handle:
        if create:
            os.chmod(lock_path, 0o600)
        fcntl.flock(handle.fileno(), fcntl.LOCK_EX if create else fcntl.LOCK_SH)
        try:
            yield
        finally:
            fcntl.flock(handle.fileno(), fcntl.LOCK_UN)


def read_events_unlocked() -> list[dict]:
    path = events_path()
    if not path.exists():
        return []
    result = []
    for line in path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        try:
            item = json.loads(line)
        except json.JSONDecodeError:
            continue
        if item.get("schema_version") == SCHEMA_VERSION:
            result.append(item)
    return result


def write_events_unlocked(events: list[dict]) -> None:
    path = events_path()
    ensure_private_dir(path.parent)
    fd, tmp_name = tempfile.mkstemp(prefix=".events.", dir=path.parent)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as handle:
            for event in events:
                handle.write(json.dumps(event, sort_keys=True, separators=(",", ":")))
                handle.write("\n")
        os.chmod(tmp_name, 0o600)
        os.replace(tmp_name, path)
    finally:
        with contextlib.suppress(FileNotFoundError):
            os.unlink(tmp_name)


def retained(events: list[dict], cfg: dict, now: dt.datetime | None = None) -> list[dict]:
    cutoff = (now or utcnow()) - dt.timedelta(days=int(cfg["retention_days"]))
    return [event for event in events if parse_time(event["timestamp"]) >= cutoff]


def prune_runtime(runtime: dict, cfg: dict, now: dt.datetime | None = None) -> dict:
    """Bound private correlation state by retention."""
    cutoff = (now or utcnow()) - dt.timedelta(days=int(cfg["retention_days"]))
    shapes = {"claude_turns": "turn"}
    for collection, required in shapes.items():
        current = runtime.get(collection)
        if not isinstance(current, dict):
            runtime[collection] = {}
            continue
        kept = {}
        for key, value in current.items():
            if not isinstance(value, dict) or required not in value:
                continue
            try:
                recent = parse_time(str(value.get("updated"))) >= cutoff
            except (TypeError, ValueError):
                recent = False
            if recent:
                kept[key] = value
        runtime[collection] = kept
    return runtime


def claude_turn(payload: dict, cfg: dict, advance: bool) -> str:
    """Supply the turn key Claude hooks do not currently expose."""
    session = anonymize(payload.get("session_id") or "unknown", cfg, "session")
    with state_lock():
        path = runtime_path()
        runtime = read_json_object(path) if path.exists() else {}
        legacy_turns = runtime.get("claude_turns")
        legacy = legacy_turns.get(session) if isinstance(legacy_turns, dict) else None
        now = utcnow()
        prune_runtime(runtime, cfg, now)
        turns = runtime.setdefault("claude_turns", {})
        entry = turns.get(session)
        if not advance and not entry and isinstance(legacy, str):
            entry = {"turn": legacy, "updated": iso(now)}
        if advance or not isinstance(entry, dict) or not entry.get("turn"):
            entry = {"turn": uuid.uuid4().hex, "updated": iso(now)}
        else:
            entry["updated"] = iso(now)
        turns[session] = entry
        atomic_json(path, runtime)
        return str(entry["turn"])


def anonymize(value: object, cfg: dict, prefix: str) -> str:
    raw = str(value or "unknown")
    digest = hashlib.sha256(f"{cfg['salt']}:{raw}".encode()).hexdigest()[:12]
    return f"{prefix}-{digest}"


def repository_key(cwd: object, cfg: dict) -> str:
    try:
        normalized = str(Path(str(cwd or ".")).expanduser().resolve())
    except OSError:
        normalized = str(cwd or "unknown")
    return anonymize(normalized, cfg, "repo")


def strip_message_content(message: str) -> str:
    without_fences = re.sub(r"```.*?```", "", message, flags=re.S)
    return re.sub(r"`[^`]*`", "", without_fences)


def detect_human_requests(message: str) -> list[dict]:
    """Return content-free request classifications from an assistant response."""
    text = strip_message_content(message or "")
    requests = []
    question_chunks = [chunk for chunk in text.split("?")[:-1] if chunk.strip()]
    for chunk in question_chunks[:20]:
        category = "policy_confirmation" if POLICY_QUESTION.search(chunk) else "clarification"
        requests.append({"category": category, "blocking": True, "signature": category})

    if not question_chunks and ACTION_REQUEST.search(text):
        requests.append(
            {"category": "action_request", "blocking": True, "signature": "action-request"}
        )

    if HANDOFF.search(text):
        requests.append({"category": "handoff", "blocking": False, "signature": "handoff"})
    return requests


def safe_command_prefix(command: object) -> tuple[list[str] | None, str | None]:
    """Return a conservative command prefix and refusal reason without retaining arguments."""
    if not isinstance(command, str) or not command.strip():
        return None, "command input is unavailable"
    if SENSITIVE.search(command):
        return None, "command may contain a credential or secret"
    if SHELL_META.search(command):
        return None, "compound or shell-expanded commands require case-by-case review"
    try:
        tokens = shlex.split(command)
    except ValueError:
        return None, "command could not be parsed safely"
    while tokens and re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*=.*", tokens[0]):
        tokens.pop(0)
    if not tokens:
        return None, "command input is unavailable"

    executable = Path(tokens[0]).name
    lower = [executable.lower(), *[token.lower() for token in tokens[1:4]]]
    dangerous = {
        "rm",
        "sudo",
        "curl",
        "wget",
        "ssh",
        "scp",
        "dd",
        "chmod",
        "chown",
    }
    if lower[0] in dangerous:
        return None, f"{lower[0]} is too broad or side-effectful for an automatic suggestion"
    if lower[0] == "git" and len(lower) > 1 and lower[1] in {
        "add",
        "commit",
        "push",
        "pull",
        "merge",
        "rebase",
        "reset",
        "checkout",
        "switch",
        "tag",
    }:
        return None, "mutating Git operations remain human-controlled"

    safe_shapes = {
        ("git", "status"): 2,
        ("git", "diff"): 2,
        ("git", "log"): 2,
        ("git", "show"): 2,
        ("npm", "test"): 2,
        ("npm", "run", "test"): 3,
        ("cargo", "test"): 2,
        ("flutter", "test"): 2,
        ("pytest",): 1,
    }
    for shape, size in safe_shapes.items():
        if tuple(lower[: len(shape)]) == shape:
            return [str(token) for token in lower[:size]], None
    return None, "no narrow, known-low-risk prefix could be derived"


def approval_signature(payload: dict) -> tuple[str, dict | None, str | None]:
    tool = str(payload.get("tool_name") or "unknown")
    tool_input = payload.get("tool_input") or {}
    if tool == "Bash":
        prefix, refusal = safe_command_prefix(tool_input.get("command"))
        if prefix:
            signature = "Bash:" + " ".join(prefix)
            return signature, {"kind": "command_prefix", "prefix": prefix}, None
        return "Bash:review-required", None, refusal
    if tool.startswith("mcp__") and re.fullmatch(r"mcp__[A-Za-z0-9_*.-]+", tool):
        return f"tool:{tool}", {"kind": "tool", "tool": tool}, None
    if re.fullmatch(r"[A-Za-z][A-Za-z0-9_.-]*", tool):
        return f"tool:{tool}", {"kind": "tool", "tool": tool}, None
    return "tool:review-required", None, "tool name could not be normalized safely"


def make_event(
    cfg: dict,
    *,
    vendor: str,
    payload: dict,
    category: str,
    blocking: bool,
    outcome: str,
    source: str,
    confidence: str,
    signature: str,
    round_trip: str | None = None,
    candidate: dict | None = None,
    refusal: str | None = None,
    timestamp: str | None = None,
    human_response: bool = False,
) -> dict:
    session_raw = payload.get("session_id") or "unknown"
    turn_raw = payload.get("turn_id") or payload.get("turnId") or "unknown"
    item_raw = payload.get("item_id") or payload.get("itemId") or payload.get("tool_use_id")
    agent_raw = payload.get("agent_id") or payload.get("agent_type")
    subagent = bool(agent_raw) or payload.get("hook_event_name") == "SubagentStop"
    return {
        "schema_version": SCHEMA_VERSION,
        "event_id": uuid.uuid4().hex,
        "timestamp": timestamp or iso(),
        "vendor": vendor,
        "source": source,
        "confidence": confidence,
        "session": anonymize(session_raw, cfg, "session"),
        "turn": anonymize(f"{session_raw}:{turn_raw}", cfg, "turn"),
        "item": anonymize(item_raw, cfg, "item") if item_raw else None,
        "workflow": anonymize(payload.get("workflow_session") or session_raw, cfg, "workflow"),
        "agent_scope": "subagent" if subagent else "root",
        "agent": anonymize(agent_raw or "root", cfg, "agent"),
        "repository": repository_key(payload.get("cwd"), cfg),
        "category": category,
        "blocking": bool(blocking),
        "outcome": outcome,
        "human_response": human_response,
        "wait_ms": None,
        "round_trip": round_trip or (uuid.uuid4().hex if blocking else None),
        "signature": signature,
        "candidate": candidate,
        "refusal": refusal,
    }


def mutate_events(mutator) -> object:
    cfg = load_config(create=True)
    with state_lock():
        events = retained(read_events_unlocked(), cfg)
        result = mutator(events, cfg)
        write_events_unlocked(events)
    return result


def append_events(new_events: list[dict]) -> None:
    def add(events, _cfg):
        events.extend(new_events)

    mutate_events(add)


def resolve_pending(
    payload: dict,
    vendor: str,
    categories: set[str],
    outcome: str,
    human_response: bool = False,
) -> int:
    cfg = load_config(create=True)
    session = anonymize(payload.get("session_id") or "unknown", cfg, "session")
    turn = anonymize(
        f"{payload.get('session_id') or 'unknown'}:"
        f"{payload.get('turn_id') or payload.get('turnId') or 'unknown'}",
        cfg,
        "turn",
    )
    signature = None
    if categories == {"approval"}:
        signature, _candidate, _refusal = approval_signature(payload)

    def resolve(events, _cfg):
        now = utcnow()
        matches = [
            event
            for event in events
            if event["vendor"] == vendor
            and event["session"] == session
            and event["category"] in categories
            and event["outcome"] == "pending"
            and (categories != {"approval"} or event["turn"] == turn)
            and (signature is None or event["signature"] == signature)
        ]
        if categories == {"approval"} and matches:
            matches = [matches[-1]]
        for event in matches:
            event["outcome"] = outcome
            event["human_response"] = human_response
            event["wait_ms"] = max(
                0, int((now - parse_time(event["timestamp"])).total_seconds() * 1000)
            )
        return len(matches)

    return int(mutate_events(resolve))


def resolve_event(event_id: str, outcome: str, human_response: bool = False) -> None:
    def resolve(events, _cfg):
        now = utcnow()
        for event in events:
            if event["event_id"] == event_id and event["outcome"] == "pending":
                event["outcome"] = outcome
                event["human_response"] = human_response
                event["wait_ms"] = max(
                    0, int((now - parse_time(event["timestamp"])).total_seconds() * 1000)
                )
                break

    mutate_events(resolve)


def app_server_outcome(result: object) -> str:
    flattened = json.dumps(result, sort_keys=True).lower()
    if isinstance(result, dict) and "permissions" in result:
        permissions = result.get("permissions")
        return "accepted" if permissions else "declined"
    if re.search(r'"(?:decline|declined|deny|denied)"', flattened):
        return "declined"
    if re.search(r'"cancel(?:led)?"', flattened):
        return "aborted"
    if re.search(r'"(?:accept(?:ed|forsession|withexecpolicyamendment)?|allow)"', flattened):
        return "accepted"
    return "unknown"


def ingest_codex_app_server(
    repository: str, client_responses_are_human: bool = False
) -> int:
    """Consume a JSONL copy of Codex app-server traffic from a custom host."""
    cfg = load_config(create=True)
    pending: dict[str, tuple[str, bool]] = {}
    recorded = 0
    resolved = 0
    request_methods = {
        "item/commandExecution/requestApproval": ("approval", "Bash"),
        "item/fileChange/requestApproval": ("approval", "apply_patch"),
        "item/permissions/requestApproval": ("approval", "request_permissions"),
        "item/tool/requestUserInput": ("clarification", "request_user_input"),
        "tool/requestUserInput": ("clarification", "request_user_input"),
        "mcpServer/elicitation/request": ("clarification", "mcp_elicitation"),
    }
    for line in sys.stdin:
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            continue
        if not isinstance(message, dict):
            continue
        method = message.get("method")
        params = message.get("params") if isinstance(message.get("params"), dict) else {}
        if method in request_methods:
            category, tool_name = request_methods[method]
            payload = {
                "session_id": params.get("threadId") or "unknown",
                "workflow_session": params.get("parentThreadId") or params.get("threadId") or "unknown",
                "turn_id": params.get("turnId") or "unknown",
                "item_id": (
                    params.get("itemId")
                    if params.get("itemId") is not None
                    else message.get("id")
                ),
                "cwd": params.get("cwd") or repository,
                "agent_id": params.get("agentId"),
                "tool_name": tool_name,
                "tool_input": {"command": params.get("command")} if tool_name == "Bash" else {},
            }
            if category == "approval":
                signature, candidate, refusal = approval_signature(payload)
            else:
                signature, candidate, refusal = method, None, None
            event = make_event(
                cfg,
                vendor="codex",
                payload=payload,
                category=category,
                blocking=True,
                outcome="pending",
                source="codex_app_server",
                confidence="authoritative_request",
                signature=signature,
                candidate=candidate,
                refusal=refusal,
            )
            append_events([event])
            raw_request_id = (
                message.get("id")
                if message.get("id") is not None
                else params.get("requestId")
            )
            request_id = str(raw_request_id if raw_request_id is not None else event["event_id"])
            pending[request_id] = (
                event["event_id"],
                params.get("autoResolutionMs") is not None,
            )
            recorded += 1
        elif "id" in message and "method" not in message and str(message["id"]) in pending:
            event_id, _auto = pending.pop(str(message["id"]))
            resolve_event(
                event_id,
                app_server_outcome(message.get("result")),
                human_response=client_responses_are_human,
            )
            resolved += 1
        elif method == "serverRequest/resolved":
            request_id = str(params.get("requestId") or "")
            if request_id in pending:
                event_id, auto = pending.pop(request_id)
                resolve_event(event_id, "auto_resolved" if auto else "aborted")
                resolved += 1
    print(f"Codex app-server metadata: recorded={recorded} resolved={resolved}")
    print("No raw JSON-RPC payload, prompt, command arguments, or response content was retained.")
    return 0


def summarize(events: list[dict]) -> dict:
    result = {
        "requests": len(events),
        "blocking_interruptions": len(
            {event["round_trip"] for event in events if event["blocking"] and event["round_trip"]}
        ),
        "round_trips": len(
            {
                event["round_trip"]
                for event in events
                if event["blocking"] and event["round_trip"] and event.get("human_response")
            }
        ),
        "approvals": 0,
        "clarifications": 0,
        "policy_confirmations": 0,
        "action_requests": 0,
        "handoffs": 0,
        "accepted": 0,
        "declined": 0,
        "aborted": 0,
        "auto_resolved": 0,
        "pending": 0,
        "unknown": 0,
        "not_required": 0,
    }
    category_keys = {
        "approval": "approvals",
        "clarification": "clarifications",
        "policy_confirmation": "policy_confirmations",
        "action_request": "action_requests",
        "handoff": "handoffs",
    }
    for event in events:
        result[category_keys[event["category"]]] += 1
        if event["outcome"] in result:
            result[event["outcome"]] += 1
    return result


def hook_output(problem: bool = False) -> None:
    output = {}
    if problem:
        output["systemMessage"] = (
            "Agent-base telemetry failed. In agent-base, diagnose and repair it or record a "
            "roadmap item; in a downstream client, prepare a repair prompt for an agent-base agent."
        )
    print(json.dumps(output, separators=(",", ":")))


def handle_hook(vendor: str) -> int:
    try:
        payload = json.load(sys.stdin)
        cfg = load_config(create=False)
        if not cfg.get("enabled") or vendor not in cfg.get("vendors", []):
            hook_output()
            return 0
        event_name = payload.get("hook_event_name")
        if vendor == "claude" and not payload.get("turn_id"):
            payload["turn_id"] = claude_turn(payload, cfg, event_name == "UserPromptSubmit")
        if event_name == "PermissionRequest":
            signature, candidate, refusal = approval_signature(payload)
            append_events(
                [
                    make_event(
                        cfg,
                        vendor=vendor,
                        payload=payload,
                        category="approval",
                        blocking=True,
                        outcome="pending",
                        source="vendor_hook",
                        confidence="authoritative_request",
                        signature=signature,
                        candidate=candidate,
                        refusal=refusal,
                    )
                ]
            )
        elif event_name in {"PostToolUse", "PostToolUseFailure"}:
            resolve_pending(payload, vendor, {"approval"}, "accepted")
        elif event_name == "UserPromptSubmit":
            resolve_pending(payload, vendor, QUESTION_CATEGORIES, "accepted", human_response=True)
        elif event_name in {"Stop", "SubagentStop"}:
            if payload.get("stop_hook_active"):
                hook_output()
                return 0
            classifications = detect_human_requests(payload.get("last_assistant_message") or "")
            if classifications:
                round_trip = anonymize(
                    f"{vendor}:{payload.get('session_id') or 'unknown'}:"
                    f"{payload.get('turn_id') or 'unknown'}:assistant-message",
                    cfg,
                    "round",
                )
                created = []
                for item in classifications:
                    created.append(
                        make_event(
                            cfg,
                            vendor=vendor,
                            payload=payload,
                            category=item["category"],
                            blocking=item["blocking"],
                            outcome="pending" if item["blocking"] else "not_required",
                            source="assistant_message_heuristic",
                            confidence="heuristic",
                            signature=item["signature"],
                            round_trip=round_trip if item["blocking"] else None,
                        )
                    )
                append_events(created)
            hook_output()
            return 0
        elif event_name == "SessionEnd":
            session = anonymize(payload.get("session_id") or "unknown", cfg, "session")

            def abort(events, _cfg):
                now = utcnow()
                for event in events:
                    if event["vendor"] == vendor and event["session"] == session and event["outcome"] == "pending":
                        event["outcome"] = "aborted"
                        event["wait_ms"] = max(
                            0,
                            int(
                                (now - parse_time(event["timestamp"])).total_seconds()
                                * 1000
                            ),
                        )

            mutate_events(abort)
        hook_output()
    except Exception:  # Hooks must never block the vendor on telemetry failure.
        print("human-interruptions: hook failed; inspect collector status", file=sys.stderr)
        hook_output(problem=True)
    return 0


def recommendation_for(vendor: str, candidate: dict) -> tuple[str | None, str, str]:
    if candidate["kind"] == "command_prefix":
        prefix = candidate["prefix"]
        if vendor == "codex":
            quoted = ", ".join(json.dumps(token) for token in prefix)
            rule = f'prefix_rule(pattern = [{quoted}], decision = "allow")'
            return rule, "project .codex/rules/default.rules; persistent after restart", (
                "Allows every command beginning with this exact argument prefix outside the sandbox."
            )
        command = " ".join(prefix)
        return f"Bash({command} *)", "project .claude/settings.json; persistent", (
            "Allows Bash commands with this prefix; deny/ask rules still take precedence."
        )
    if candidate["kind"] == "tool":
        return None, "session only", (
            "A whole-tool grant has no verified operation-specific boundary and is too broad "
            "for a persistent recommendation."
        )
    return None, "session only", "No verified narrow persistent rule is available for this tool."


def recommendations(events: list[dict], minimum: int = 2) -> list[dict]:
    groups: dict[tuple[str, str, str], list[dict]] = {}
    for event in events:
        if event["category"] != "approval":
            continue
        key = (event["vendor"], event["repository"], event["signature"])
        groups.setdefault(key, []).append(event)
    result = []
    for (vendor, repository, signature), members in groups.items():
        if len(members) < minimum:
            continue
        candidate = next((event.get("candidate") for event in members if event.get("candidate")), None)
        refusal = next((event.get("refusal") for event in members if event.get("refusal")), None)
        if not candidate:
            result.append(
                {
                    "vendor": vendor,
                    "repository": repository,
                    "signature": signature,
                    "occurrences": len(members),
                    "expected_reduction": 0,
                    "status": "refused",
                    "reason": refusal or "no narrow candidate is available",
                }
            )
            continue
        rule, scope, tradeoff = recommendation_for(vendor, candidate)
        if not rule:
            result.append(
                {
                    "vendor": vendor,
                    "repository": repository,
                    "signature": signature,
                    "occurrences": len(members),
                    "expected_reduction": 0,
                    "status": "refused",
                    "reason": tradeoff,
                }
            )
            continue
        result.append(
            {
                "vendor": vendor,
                "repository": repository,
                "signature": signature,
                "occurrences": len(members),
                "expected_reduction": len(members),
                "status": "review",
                "rule": rule,
                "scope": scope,
                "security_tradeoff": tradeoff,
            }
        )
    return sorted(result, key=lambda item: (-item["occurrences"], item["vendor"], item["signature"]))


def filtered_events(days: int, vendor: str | None, repository: str | None) -> list[dict]:
    cfg = load_config(create=False)
    cutoff = utcnow() - dt.timedelta(days=days)
    if not events_path().exists():
        events = []
    else:
        with state_lock(create=False):
            events = retained(read_events_unlocked(), cfg)
    return [
        event
        for event in events
        if parse_time(event["timestamp"]) >= cutoff
        and (not vendor or event["vendor"] == vendor)
        and (not repository or event["repository"] == repository)
    ]


def print_report(args) -> int:
    events = filtered_events(args.days, args.vendor, args.repository)
    totals = summarize(events)
    print(f"Human interruptions ({args.days} day(s))")
    print(
        "  requests={requests} blocking_interruptions={blocking_interruptions} "
        "human_round_trips={round_trips} approvals={approvals} "
        "clarifications={clarifications} policy_confirmations={policy_confirmations} "
        "action_requests={action_requests} handoffs={handoffs}".format(**totals)
    )
    print(
        "  accepted={accepted} declined={declined} aborted={aborted} "
        "auto_resolved={auto_resolved} pending={pending} unknown={unknown} "
        "not_required={not_required}".format(**totals)
    )
    waits = [event["wait_ms"] for event in events if event.get("wait_ms") is not None]
    if waits:
        print(
            f"  measured_waits={len(waits)} average_wait_ms={sum(waits) // len(waits)} "
            f"maximum_wait_ms={max(waits)}"
        )
    by_vendor = {}
    by_repo = {}
    by_scope = {}
    by_workflow = {}
    by_signature = {}
    for event in events:
        by_vendor[event["vendor"]] = by_vendor.get(event["vendor"], 0) + 1
        by_repo[event["repository"]] = by_repo.get(event["repository"], 0) + 1
        by_scope[event["agent_scope"]] = by_scope.get(event["agent_scope"], 0) + 1
        by_workflow[event["workflow"]] = by_workflow.get(event["workflow"], 0) + 1
        by_signature[event["signature"]] = by_signature.get(event["signature"], 0) + 1
    print(f"  by_vendor={json.dumps(by_vendor, sort_keys=True)}")
    print(f"  by_repository={json.dumps(by_repo, sort_keys=True)}")
    print(f"  by_agent_scope={json.dumps(by_scope, sort_keys=True)}")
    print(f"  by_workflow={json.dumps(by_workflow, sort_keys=True)}")
    print(f"  by_signature={json.dumps(by_signature, sort_keys=True)}")
    if args.recommendations:
        print_recommendations(recommendations(events, args.minimum))
    return 0


def print_recommendations(items: list[dict]) -> None:
    if not items:
        print("Recommendations: none (need repeated normalized approvals).")
        return
    print("Recommendations (review only; nothing was applied):")
    for item in items:
        prefix = f"  {item['vendor']} {item['repository']} x{item['occurrences']}"
        if item["status"] == "refused":
            print(f"{prefix}: REFUSED - {item['reason']}")
        else:
            print(f"{prefix}: {item['rule']}")
            print(
                f"    scope={item['scope']}; expected_reduction={item['expected_reduction']}; "
                f"tradeoff={item['security_tradeoff']}"
            )


def command_path() -> str:
    return str(installed_script())


def hook_command(vendor: str) -> str:
    return f'python3 "{command_path()}" hook --vendor {vendor}'


def vendor_settings_path(vendor: str) -> Path:
    if vendor == "codex":
        codex_root = Path(
            os.environ.get(
                "AB_HUMAN_INTERRUPTION_CODEX_CONFIG_HOME",
                Path.home() / ".codex",
            )
        )
        return codex_root / "hooks.json"
    claude_root = Path(
        os.environ.get(
            "AB_HUMAN_INTERRUPTION_CLAUDE_CONFIG_HOME",
            os.environ.get("CLAUDE_CONFIG_DIR", Path.home() / ".claude"),
        )
    )
    return claude_root / "settings.json"


def read_json_object(path: Path) -> dict:
    if not path.exists():
        return {}
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise RuntimeError(f"cannot read {path}: {exc}") from exc
    if not isinstance(value, dict):
        raise RuntimeError(f"{path} must contain a JSON object")
    return value


def owned_hook(entry: object, vendor: str) -> bool:
    if not isinstance(entry, dict):
        return False
    hooks = entry.get("hooks")
    if not isinstance(hooks, list) or not hooks:
        return False
    command = hook_command(vendor)
    return all(isinstance(hook, dict) and hook.get("command") == command for hook in hooks)


def update_vendor_hooks(vendor: str, install: bool) -> tuple[Path, bool]:
    path = vendor_settings_path(vendor)
    data = read_json_object(path)
    hooks = data.setdefault("hooks", {})
    if not isinstance(hooks, dict):
        raise RuntimeError(f"{path}: hooks must be a JSON object")
    changed = False
    for event_name in HOOK_EVENTS[vendor]:
        entries = hooks.get(event_name, [])
        if not isinstance(entries, list):
            raise RuntimeError(f"{path}: hooks.{event_name} must be a JSON array")
        desired = [entry for entry in entries if not owned_hook(entry, vendor)]
        if install:
            desired.append(
                {
                    "hooks": [
                        {
                            "type": "command",
                            "command": hook_command(vendor),
                            "timeout": 3,
                        }
                    ]
                }
            )
        if desired != entries:
            changed = True
        if desired:
            hooks[event_name] = desired
        elif event_name in hooks:
            del hooks[event_name]
            changed = True
    if not hooks:
        data.pop("hooks", None)
    if changed:
        atomic_json(path, data)
    return path, changed


def vendor_hook_count(vendor: str) -> int:
    data = read_json_object(vendor_settings_path(vendor))
    hooks = data.get("hooks", {})
    if not isinstance(hooks, dict):
        return 0
    return sum(
        1
        for entries in hooks.values()
        if isinstance(entries, list)
        for entry in entries
        if owned_hook(entry, vendor)
    )


def installed_vendors() -> set[str]:
    return {vendor for vendor in SUPPORTED_VENDORS if vendor_hook_count(vendor)}


def install_collector(vendors: list[str]) -> int:
    source = Path(__file__).resolve()
    cfg = load_config(create=True)
    selected_state = install_state_dir(cfg)
    ensure_private_dir(selected_state)
    cfg["state_dir"] = str(selected_state)
    destination = installed_script()
    ensure_private_dir(destination.parent)
    copy_needed = not destination.exists() or source.read_bytes() != destination.read_bytes()
    if copy_needed:
        shutil.copy2(source, destination)
        destination.chmod(0o700)
    configured = (
        set(cfg.get("vendors", [])) & installed_vendors()
        if cfg.get("enabled")
        else set()
    )
    configured.update(vendors)
    cfg["enabled"] = True
    cfg["vendors"] = sorted(configured)
    atomic_json(config_path(), cfg)
    print(f"{'Updated' if copy_needed else 'Up to date'}: {destination}")
    for vendor in vendors:
        path, changed = update_vendor_hooks(vendor, True)
        print(f"{'Updated' if changed else 'Up to date'}: {path} ({vendor} hooks)")
    print(
        f"Enabled: {', '.join(cfg['vendors'])}; state: {events_path()} "
        f"(retention {cfg['retention_days']} days)"
    )
    print("No permission rule was added or changed.")
    return 0


def disable_collector() -> int:
    cfg = load_config(create=True)
    cfg["enabled"] = False
    atomic_json(config_path(), cfg)
    print(f"Disabled collector in {config_path()}; hooks and data remain auditable.")
    return 0


def uninstall_collector(vendors: list[str], purge_data: bool) -> int:
    cfg = load_config(create=True)
    configured = set(cfg.get("vendors", [])) & installed_vendors()
    for vendor in vendors:
        path, changed = update_vendor_hooks(vendor, False)
        print(f"{'Updated' if changed else 'Up to date'}: {path} ({vendor} hooks removed)")
    remaining_hooks = installed_vendors()
    remaining_configured = (configured - set(vendors)) & remaining_hooks
    cfg["vendors"] = sorted(remaining_configured)
    cfg["enabled"] = bool(cfg.get("enabled") and remaining_configured)
    atomic_json(config_path(), cfg)
    destination = installed_script()
    if remaining_hooks:
        print(
            f"Preserved shared collector: {destination} "
            f"({', '.join(sorted(remaining_hooks))} hooks remain)."
        )
    else:
        with contextlib.suppress(FileNotFoundError):
            destination.unlink()
            print(f"Removed: {destination}")
    if purge_data and remaining_hooks:
        print(
            "Skipped --purge-data because another vendor still has installed hooks; "
            f"preserved: {events_path()}"
        )
    elif purge_data:
        for path in (events_path(), runtime_path()):
            with contextlib.suppress(FileNotFoundError):
                path.unlink()
                print(f"Removed telemetry data: {path}")
    else:
        print(f"Preserved telemetry data: {events_path()}")
    return 0


def status_collector() -> int:
    cfg = load_config(create=False)
    configured = sorted(
        vendor for vendor in cfg.get("vendors", []) if vendor in SUPPORTED_VENDORS
    )
    print(
        f"enabled={bool(cfg.get('enabled'))} vendors={','.join(configured) or 'none'} "
        f"retention_days={cfg.get('retention_days')}"
    )
    state_ready = state_dir().is_dir() and os.access(state_dir(), os.R_OK | os.W_OK)
    print(
        f"config={config_path()} state={events_path()} state_ready={state_ready} "
        f"installed_script={installed_script()}"
    )
    for vendor in SUPPORTED_VENDORS:
        path = vendor_settings_path(vendor)
        count = vendor_hook_count(vendor)
        print(f"{vendor}: hooks={count}/{len(HOOK_EVENTS[vendor])} path={path}")
    return 0


def manual_record(args) -> int:
    cfg = load_config(create=True)
    payload = {
        "session_id": args.session,
        "turn_id": args.turn,
        "cwd": args.cwd,
        "agent_id": args.agent if args.agent_scope == "subagent" else None,
    }
    events = [
        make_event(
            cfg,
            vendor=args.vendor,
            payload=payload,
            category=args.category,
            blocking=args.blocking,
            outcome=args.outcome,
            source="explicit_adapter",
            confidence="adapter_reported",
            signature=args.signature,
            round_trip=(
                anonymize(args.round_trip, cfg, "round") if args.round_trip else None
            ),
        )
        for _ in range(args.count)
    ]
    append_events(events)
    print(f"Recorded {len(events)} content-free event(s).")
    return 0


def selected_vendors(value: str) -> list[str]:
    return list(SUPPORTED_VENDORS) if value == "all" else [value]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    hook = sub.add_parser("hook", help="Consume one vendor hook JSON object from stdin")
    hook.add_argument("--vendor", required=True, choices=SUPPORTED_VENDORS)

    report = sub.add_parser("report", help="Print aggregate content-free counts")
    report.add_argument("--days", type=int, default=7)
    report.add_argument("--vendor", choices=SUPPORTED_VENDORS)
    report.add_argument("--repository", help="An anonymized repository key from a prior report")
    report.add_argument("--recommendations", action="store_true")
    report.add_argument("--minimum", type=int, default=2)

    recommend = sub.add_parser("recommend", help="Print review-only permission recommendations")
    recommend.add_argument("--days", type=int, default=30)
    recommend.add_argument("--vendor", choices=SUPPORTED_VENDORS)
    recommend.add_argument("--repository")
    recommend.add_argument("--minimum", type=int, default=2)

    record = sub.add_parser("record", help="Record a content-free event from another adapter")
    record.add_argument("--vendor", required=True, choices=SUPPORTED_VENDORS)
    record.add_argument("--category", required=True, choices=sorted(CATEGORIES))
    record.add_argument("--outcome", default="pending", choices=sorted(OUTCOMES))
    record.add_argument("--blocking", action="store_true")
    record.add_argument("--session", required=True)
    record.add_argument("--turn", default="unknown")
    record.add_argument("--cwd", default=".")
    record.add_argument("--agent-scope", choices=("root", "subagent"), default="root")
    record.add_argument("--agent", default="adapter")
    record.add_argument("--signature", default="adapter-reported")
    record.add_argument("--round-trip")
    record.add_argument("--count", type=int, default=1)

    app_server = sub.add_parser(
        "ingest-codex-app-server",
        help="Consume a JSONL copy of Codex app-server traffic from a custom host",
    )
    app_server.add_argument("--repository", default=".")
    app_server.add_argument(
        "--client-responses-are-human",
        action="store_true",
        help="Count client responses as human round trips when the host guarantees that boundary",
    )

    for name in ("install", "update"):
        command = sub.add_parser(name, help=f"Explicitly {name} machine-wide hooks")
        command.add_argument("--vendor", choices=(*SUPPORTED_VENDORS, "all"), default="all")

    sub.add_parser("disable", help="Disable collection without removing hooks or data")
    uninstall = sub.add_parser("uninstall", help="Remove owned machine-wide hooks and script")
    uninstall.add_argument("--vendor", choices=(*SUPPORTED_VENDORS, "all"), default="all")
    uninstall.add_argument("--purge-data", action="store_true")
    sub.add_parser("status", help="Audit installation and enablement state")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.command == "hook":
        return handle_hook(args.vendor)
    if args.command == "report":
        return print_report(args)
    if args.command == "recommend":
        events = filtered_events(args.days, args.vendor, args.repository)
        print_recommendations(recommendations(events, args.minimum))
        return 0
    if args.command == "record":
        return manual_record(args)
    if args.command == "ingest-codex-app-server":
        return ingest_codex_app_server(
            args.repository, args.client_responses_are_human
        )
    if args.command in {"install", "update"}:
        return install_collector(selected_vendors(args.vendor))
    if args.command == "disable":
        return disable_collector()
    if args.command == "uninstall":
        return uninstall_collector(selected_vendors(args.vendor), args.purge_data)
    if args.command == "status":
        return status_collector()
    return 2


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except RuntimeError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
