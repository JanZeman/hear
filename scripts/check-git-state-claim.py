#!/usr/bin/env python3
"""check-git-state-claim.py - Stop hook enforcing AV-GIT-003 (fresh Git state before a claim).

An agent that remembers "my changes are still uncommitted" from earlier in the session reports it
as fact, although the human (or a worktree sync) may have committed or moved them since. This hook
reacts to the agent's own outgoing message: when it states Git state (committed, pushed, clean,
ahead/behind, untracked, ...) and no read-only Git check ran in the current turn after the turn's
last possibly state-changing action, it blocks the stop once and asks the agent to re-check and
correct itself.

It also checks a shown AV-GIT-002 commit block against the repository it names, since a block in
a fence is not a prose claim: when that repository has nothing to commit (and nothing to push), the
block is stale, usually because the human already committed from their own terminal, and the hook
asks the agent to drop it instead of announcing a missing commit.

Usage (from the vendor's Stop hook, payload JSON on stdin):
    python3 check-git-state-claim.py claude|codex

Safety, same as every other ai-venture hook:
  - Fires at most once per stop: a continuation already caused by a Stop hook
    (`stop_hook_active`) is always let through, so a bug here cannot loop a session.
  - Always exits 0. Anything unexpected (unreadable payload, missing message) means silence.
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
from pathlib import Path

CLAIM_PATTERNS = [
    # English
    r"\buncommitted\b",
    r"\bnot\s+(?:yet\s+)?(?:been\s+)?(?:committed|pushed|staged)\b",
    r"\bunpushed\b",
    r"\bunstaged\b",
    r"\buntracked\b",
    r"\b(?:already|now|been|were|was|is|are)\s+(?:committed|pushed|staged)\b",
    r"\b(?:working\s+tree|worktree|repo(?:sitory)?|branch)\s+(?:is\s+|are\s+)?(?:clean|dirty)\b",
    r"\bnothing\s+to\s+commit\b",
    r"\b(?:ahead|behind)\s+(?:of\s+)?(?:origin|the\s+remote|upstream|main|master)\b",
    r"\bup\s+to\s+date\s+with\s+(?:origin|the\s+remote|upstream)\b",
    r"\blocal\s+(?:changes|commits)\b",
    r"\b(?:still\s+)?needs?\s+(?:to\s+be\s+)?(?:a\s+)?(?:committed|pushed|commit|push)\b",
    # Czech (diacritics optional, since dictated or typed text often drops them)
    r"\bne?commitnut",
    r"\bne?pushnut",
    r"\bne?commitovan",
    r"\bne?pushovan",
    r"\bne?stagovan",
    r"\bnesledovan",
    r"\b(?:worktree|repo|repozit[aá][rř]|v[eě]tev)\w*\s+(?:je\s+|jsou\s+)?(?:[cč]ist|[sš]pinav)",
    r"\bnen[ií]\s+co\s+commitnout\b",
    r"\b(?:p[rř]ed|za)\s+(?:origin|remote|upstream)\b",
    r"\bzb[yý]v[aá]\s+(?:u[zž]\s+)?(?:jen\s+|pouze\s+)?(?:commit|push)",
]
CLAIM_RE = re.compile("|".join(CLAIM_PATTERNS), re.IGNORECASE)

# Read-only commands that observe Git state.
GIT_READ_RE = re.compile(
    r"\bgit\b(?:\s+-C\s+\S+)?(?:\s+--?[\w-]+(?:=\S+)?)*\s+"
    r"(?:status|diff|log|show|rev-parse|rev-list|branch|worktree\s+list|ls-files|describe|cherry)\b"
)
# Commands that may change Git state or the working tree it reports on.
GIT_MUTATE_RE = re.compile(
    r"\bgit\b(?:\s+-C\s+\S+)?(?:\s+--?[\w-]+(?:=\S+)?)*\s+"
    r"(?:add|commit|push|pull|fetch|merge|rebase|reset|checkout|switch|stash|restore|clean|rm|mv|"
    r"cherry-pick|revert|tag|am|apply|worktree\s+(?:add|remove|move|prune)|submodule\s+update)\b"
    r"|\bworktrees\.sh\b|\bapply-ai-venture\.sh\b|\bab\.sh\b|\bab-update(?:-apply)?\.sh\b"
    r"|\brun-confirmed-commit\.sh\b|\bpublish-av-release\.py\b"
)
EDIT_TOOLS = {"Edit", "Write", "MultiEdit", "NotebookEdit", "apply_patch"}
FENCE_RE = re.compile(r"(```|~~~).*?(?:\1|\Z)", re.DOTALL)

REASON = (
    "AV-GIT-003: your reply states Git state ({phrase!r}), but no read-only Git check "
    "(git status, git log, git diff, git worktree list) ran in this turn after your last action "
    "that could change it. Remembered state may be stale: the human or a sync may have committed, "
    "pushed, or moved work since. Re-check now every repository and worktree your reply mentions. "
    "If the check confirms the reply, end without repeating it; if anything differs, send a short "
    "correction in the conversation's language. Do not mention this hook."
)


BLOCK_REASON = (
    "AV-GIT-002: your reply shows a commit block for {root}, but a fresh `git status` there finds "
    "{state}. The human has most likely already run it. Drop the block and the claim that a commit "
    "or push is missing; say in one line, in the conversation's language, that the work is already "
    "{done}, and keep your next-step section. Do not mention this hook."
)
# A shown block: a `cd` line naming the root, then `git commit` inside the same fence.
COMMIT_BLOCK_RE = re.compile(
    r"(```|~~~)[^\n]*\n(?P<body>.*?)(?:\1|\Z)", re.DOTALL
)
CD_RE = re.compile(r"^\s*cd\s+(?:\"([^\"]+)\"|'([^']+)'|(\S+))\s*$", re.MULTILINE)


def _git(root: str, *args: str) -> str | None:
    try:
        proc = subprocess.run(
            ["git", "-C", root, *args], capture_output=True, text=True, timeout=5, check=False
        )
    except (OSError, subprocess.SubprocessError):
        return None
    return proc.stdout if proc.returncode == 0 else None


def stale_commit_block(message: str) -> str | None:
    """Return a block reason when a shown commit block names a repository with nothing pending."""
    for fence in COMMIT_BLOCK_RE.finditer(message or ""):
        body = fence.group("body")
        if not re.search(r"\bgit\s+commit\b", body):
            continue
        cd = CD_RE.search(body)
        if not cd:
            continue
        root = next(group for group in cd.groups() if group)
        if not Path(root).is_dir() or _git(root, "rev-parse", "--is-inside-work-tree") is None:
            continue
        status = _git(root, "status", "--porcelain")
        if status is None or status.strip():
            continue
        ahead = _git(root, "rev-list", "--count", "@{upstream}..HEAD")
        if ahead is not None and ahead.strip() not in ("", "0"):
            return BLOCK_REASON.format(
                root=root,
                state="nothing to commit, but unpushed commits",
                done="committed; only `git push` remains, so show just that",
            )
        return BLOCK_REASON.format(root=root, state="nothing to commit or push", done="committed and pushed")
    return None


def find_claim(message: str) -> str | None:
    match = CLAIM_RE.search(FENCE_RE.sub(" ", message or ""))
    return match.group(0) if match else None


def _read_jsonl(path: str | None) -> list[dict]:
    if not path:
        return []
    try:
        lines = Path(path).expanduser().read_text(encoding="utf-8", errors="replace").splitlines()
    except OSError:
        return []
    entries = []
    for line in lines:
        try:
            entry = json.loads(line)
        except ValueError:
            continue
        if isinstance(entry, dict):
            entries.append(entry)
    return entries


def _command_text(value) -> str:
    if isinstance(value, list):
        return " ".join(str(part) for part in value)
    return str(value or "")


def _events(entry: dict) -> list[tuple[str, str]]:
    """Return ("user"|"tool"|"assistant", text) events for one transcript line, either vendor."""
    events: list[tuple[str, str]] = []
    # Claude Code transcript lines.
    kind = entry.get("type")
    message = entry.get("message")
    if entry.get("isMeta") or entry.get("isSidechain"):
        return events
    if kind in ("user", "assistant") and isinstance(message, dict):
        content = message.get("content")
        if kind == "user":
            if isinstance(content, str):
                events.append(("user", content))
            elif isinstance(content, list) and any(
                isinstance(b, dict) and b.get("type") == "text" for b in content
            ) and not any(isinstance(b, dict) and b.get("type") == "tool_result" for b in content):
                events.append(("user", ""))
        elif isinstance(content, list):
            for block in content:
                if not isinstance(block, dict):
                    continue
                if block.get("type") == "tool_use":
                    name = str(block.get("name", ""))
                    tool_input = block.get("input") or {}
                    events.append(("tool", f"{name}\x00{_command_text(tool_input.get('command'))}"))
                elif block.get("type") == "text":
                    events.append(("assistant", str(block.get("text", ""))))
        return events
    # Codex rollout lines.
    payload = entry.get("payload")
    if not isinstance(payload, dict):
        return events
    ptype = payload.get("type")
    if kind == "event_msg" and ptype == "user_message":
        events.append(("user", str(payload.get("message", ""))))
    elif kind == "response_item" and ptype in ("function_call", "custom_tool_call", "local_shell_call"):
        name = str(payload.get("name", "shell"))
        raw = payload.get("arguments") or payload.get("input") or payload.get("action") or ""
        command = raw
        if isinstance(raw, str):
            try:
                parsed = json.loads(raw)
                command = parsed.get("command", parsed.get("cmd", raw)) if isinstance(parsed, dict) else raw
            except ValueError:
                command = raw
        elif isinstance(raw, dict):
            command = raw.get("command", raw.get("cmd", ""))
        events.append(("tool", f"{name}\x00{_command_text(command)}"))
    return events


def fresh_check_ran(transcript: list[dict]) -> bool | None:
    """True/False when the current turn can be judged; None when the transcript says nothing."""
    events = [event for entry in transcript for event in _events(entry)]
    starts = [i for i, (kind, _) in enumerate(events) if kind == "user"]
    if not starts:
        return None
    last_read = last_change = -1
    for offset, (kind, text) in enumerate(events[starts[-1] + 1 :]):
        if kind != "tool":
            continue
        name, _, command = text.partition("\x00")
        if name in EDIT_TOOLS or GIT_MUTATE_RE.search(command):
            last_change = offset
        # A read in the same command as a change counts only when it comes after it.
        read = list(GIT_READ_RE.finditer(command))
        change = list(GIT_MUTATE_RE.finditer(command))
        if read and (not change or read[-1].start() > change[-1].start()):
            last_read = offset
    return last_read >= 0 and last_read >= last_change


def last_assistant_text(transcript: list[dict]) -> str:
    texts = [text for entry in transcript for kind, text in _events(entry) if kind == "assistant"]
    return texts[-1] if texts else ""


def decide(payload: dict) -> str | None:
    if payload.get("stop_hook_active"):
        return None
    transcript = _read_jsonl(payload.get("transcript_path"))
    message = payload.get("last_assistant_message")
    if not isinstance(message, str):
        message = last_assistant_text(transcript)
    stale = stale_commit_block(message)
    if stale:
        return stale
    phrase = find_claim(message)
    if not phrase:
        return None
    if fresh_check_ran(transcript):
        return None
    return REASON.format(phrase=phrase)


def main(argv: list[str]) -> int:
    vendor = argv[1] if len(argv) > 1 else "claude"
    try:
        payload = json.loads(sys.stdin.read() or "{}")
        reason = decide(payload) if isinstance(payload, dict) else None
    except Exception:  # A hook bug must never trap or break the session.
        reason = None
    if reason:
        print(json.dumps({"decision": "block", "reason": reason}))
    elif vendor == "codex":
        print("{}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
