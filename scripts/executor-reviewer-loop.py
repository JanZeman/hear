#!/usr/bin/env python3
"""Bounded cross-vendor Executor/Reviewer review loop (roadmap item 122).

One growing scratch file carries the whole loop's state: the original task, written once, then
one appended section per round. Every round -- both roles, every time -- is a fresh, non-
interactive vendor CLI invocation launched via the same verified launch contracts skill 002's
`client-rooted-execution.md` already establishes; nothing here is a live, interactive session.
The Reviewer emits a `## Reviewer Findings` Markdown section (see `parse_findings`) with no
field required, not even on a structured finding -- a Reviewer may write pure prose.
"""

from __future__ import annotations

import argparse
import hashlib
import os
import re
import importlib.util
import subprocess
import sys
import tempfile
from pathlib import Path


def _invariants_module():
    """The shared launch-invariant checker, loaded by path like everything else here."""
    path = Path(__file__).resolve().with_name("vendor_launch.py")
    spec = importlib.util.spec_from_file_location("vendor_launch", path)
    existing = sys.modules.get("vendor_launch")
    if existing is not None:
        return existing
    module = importlib.util.module_from_spec(spec)
    sys.modules["vendor_launch"] = module
    spec.loader.exec_module(module)
    return module


_invariants = _invariants_module()

# Only these two vendors have verified non-interactive launch contracts today (skill 002's
# client-rooted-execution.md). Adding a third here without updating the "other one" default
# resolution below would make that default ambiguous -- see roadmap item 122's own note.
VENDORS = ("claude", "codex")

FIELD_RE = re.compile(
    r"^-\s*\*\*(File|Line|Category|Severity|Summary|Failure scenario)\*\*:\s*(.*)$",
    re.IGNORECASE,
)
FINDING_SPLIT_RE = re.compile(r"^###\s*Finding\s+\d+\s*$", re.MULTILINE)
FINDINGS_HEADING_RE = re.compile(r"^##\s*Reviewer Findings\s*$", re.MULTILINE)
# The exact convergence sentences, normalized. Kept deliberately small: anything a Reviewer writes
# beyond one of these is treated as a finding rather than guessed at. SKILL.md documents the
# sentence and requires it to stand alone.
CONVERGED_TEXTS = frozenset({"no findings", "none", "no findings found"})
NEXT_H2_RE = re.compile(r"^##\s+\S", re.MULTILINE)


def normalize(text: str) -> str:
    """Lowercase, drop punctuation, collapse whitespace -- a Reviewer re-raising the same finding
    across rounds rarely reuses identical wording verbatim, so fingerprinting must tolerate
    trivial case/punctuation drift or oscillation detection would silently never fire."""
    text = re.sub(r"[^\w\s]", "", text.strip().lower())
    return re.sub(r"\s+", " ", text)


def fingerprint(finding: dict) -> str:
    """One stable key per finding, structured fields first, free text as the fallback."""
    category = finding.get("category", "")
    file_ = finding.get("file", "")
    if category or file_:
        summary = finding.get("summary") or finding.get("body", "")
        key = f"{category}|{file_}|{normalize(summary)[:80]}"
    else:
        key = normalize(finding.get("body", ""))[:120]
    return hashlib.sha1(key.encode("utf-8")).hexdigest()[:12]


def parse_findings(reviewer_text: str) -> list[dict] | None:
    """None means the Reviewer's output had no parseable '## Reviewer Findings' section at all
    (malformed output, not zero findings). An empty list means genuine convergence."""
    m = FINDINGS_HEADING_RE.search(reviewer_text)
    if not m:
        return None
    section = reviewer_text[m.end():]
    m2 = NEXT_H2_RE.search(section)
    if m2:
        section = section[: m2.start()]
    if not FINDING_SPLIT_RE.search(section):
        # Absence of "### Finding N" is not convergence. This skill's own contract says a finding
        # carrying no structured field at all, pure prose describing a problem, is fully valid, so
        # a section holding such prose used to be read as "No findings." and ended the loop while
        # reporting the Executor's work as accepted. A reviewer raising a blocking objection in one
        # plain sentence was silently converted into approval.
        #
        # Convergence therefore has to be the narrow case and everything else a finding. The two
        # mistakes are not symmetric: calling a satisfied review a finding costs one extra round
        # that the Executor answers with "nothing to fix", visible in the log and bounded by the
        # round cap, while calling an objection convergence ends the loop on a wrong answer that
        # nobody sees.
        normalized = normalize(section)
        if not normalized:
            # An empty or punctuation-only section is a truncated or failed review, not agreement.
            # Convergence has to be stated, never inferred from an absence, or a review that died
            # mid-write is indistinguishable from one that approved the work.
            return None
        if normalized in CONVERGED_TEXTS:
            return []
        return [{"body": section.strip()}]

    parts = FINDING_SPLIT_RE.split(section)
    findings = []
    for part in parts[1:]:
        finding: dict = {}
        remaining_lines = []
        for line in part.strip().splitlines():
            fm = FIELD_RE.match(line.strip())
            if fm:
                key = fm.group(1).lower().replace(" ", "_")
                finding[key] = fm.group(2).strip()
            else:
                remaining_lines.append(line)
        body = "\n".join(remaining_lines).strip()
        # Only fall back to the whole raw block when nothing structured was recognized at all --
        # otherwise `body` would just duplicate the fields already extracted above.
        finding["body"] = body if (body or finding) else part.strip()
        findings.append(finding)
    return findings


def launch(vendor: str, role: str, root: Path, prompt_text: str) -> tuple[str, bool]:
    """Every round is a fresh non-interactive subprocess; nothing here is a live session.

    Returns the output and whether the invocation actually succeeded. The two are separate because
    a crashed or truncated run still produces text, and text that happens to contain the
    convergence marker must never be accepted as a result.
    """
    if vendor == "claude":
        mode = "auto" if role == "executor" else "dontAsk"
        cmd = ["claude", "--print", "--permission-mode", mode, "--no-session-persistence"]
        if role == "reviewer":
            # The Reviewer inspects and runs non-mutating checks, so it keeps its default tools:
            # a read-only allowlist would take Bash away and with it the ability to run the checks
            # its findings are supposed to rest on. Dropping external tool servers costs it nothing
            # and narrows what it may attempt, which is the same defence in depth the fleet auditor
            # and the staffing adapter now carry.
            cmd.append("--strict-mcp-config")
    elif vendor == "codex":
        # --skip-git-repo-check: Codex otherwise refuses to run in a directory it does not trust
        # (any non-git directory, or a git repo it has not seen before). Harmless when already
        # inside a trusted git repo -- the common real case -- but required for a bare directory
        # like a throwaway smoke-test fixture.
        if role == "executor":
            cmd = [
                "codex", "exec", "--approve-for-me", "-C", str(root),
                "--ephemeral", "--skip-git-repo-check", "-",
            ]
        else:
            cmd = [
                "codex", "exec", "-C", str(root), "--ephemeral", "--skip-git-repo-check",
                "--sandbox", "read-only", "--config", 'approval_policy="never"',
                "--ignore-user-config", "-",
            ]
    else:
        raise ValueError(f"unsupported vendor: {vendor}")

    # The Reviewer keeps its default tools, so it is checked with restrict_tools=False; everything
    # else about its command is held to the same invariants as every other launch here.
    _invariants.check(vendor, cmd, may_write=(role == "executor"),
                      restrict_tools=(role != "reviewer"))
    result = subprocess.run(
        cmd, input=prompt_text, capture_output=True, text=True, cwd=str(root)
    )
    output = result.stdout or ""
    if result.returncode != 0:
        output += f"\n\n[{vendor} {role} exited {result.returncode}]\n{result.stderr}"
    return output, result.returncode == 0


EXECUTOR_PROMPT = """\
You are the Executor in a bounded Executor/Reviewer review loop. Your working directory IS the
project root. Do the actual work in real project files -- this is not a dry run.

# Original task
{task}

# Session log so far
{log}

# Your job this round
{instructions}

When done, write a plain-text report of what you did this round: what changed, evidence (diffs,
test/check output you ran), and your own open questions or self-assessed risk. This report is
appended verbatim as this round's log entry -- do not wrap it in any special format, just write
it clearly. Do not stage, commit, or push anything unless the task explicitly says otherwise.
"""

REVIEWER_PROMPT = """\
You are the Reviewer in a bounded Executor/Reviewer review loop. Your working directory IS the
project root. You are READ-ONLY: inspect files and run non-mutating checks, but never edit,
stage, commit, or push anything.

# Original task
{task}

# Session log so far
{log}

# Your job this round
Inspect the actual current state of the project (diffs, file contents, test/check output) against
the original task and the Executor's own claims in the log above. Then write exactly one
`## Reviewer Findings` section.

If everything is satisfactory, write:
## Reviewer Findings

No findings.

Otherwise, write one `### Finding N` subsection per issue. No field is required -- use whichever
of these bold-labeled lines actually help, or write pure prose with none of them:
- **File**: relative/path
- **Line**: a line number
- **Category**: e.g. correctness, simplification, efficiency
- **Severity**: e.g. high, medium, low
- **Summary**: one-sentence claim
- **Failure scenario**: concrete inputs/state -> wrong output or crash

A finding with no bold-labeled fields at all, just prose describing the problem, is fully valid.
"""


def build_prompt(role: str, task: str, log_text: str, round_num: int) -> str:
    if role == "executor":
        if round_num == 1:
            instructions = "Complete the original task above from scratch."
        else:
            instructions = (
                "Address the most recent '## Reviewer Findings' section in the log above. For "
                "each finding, either fix it or explain why you did not."
            )
        return EXECUTOR_PROMPT.format(task=task, log=log_text, instructions=instructions)
    return REVIEWER_PROMPT.format(task=task, log=log_text)


def append_round(log_path: Path, round_num: int, label: str, text: str) -> None:
    with log_path.open("a", encoding="utf-8") as fh:
        fh.write(f"\n## Round {round_num} - {label}\n\n{text.strip()}\n")


def resolve_vendors(args: argparse.Namespace) -> tuple[str, str]:
    executor = args.executor or os.environ.get("AGENT_BASE_VENDOR")
    if executor not in VENDORS:
        sys.exit(
            "error: --executor not given and AGENT_BASE_VENDOR is unset or not one of "
            f"{VENDORS}; pass --executor explicitly"
        )
    reviewer = args.reviewer
    if not reviewer:
        others = [v for v in VENDORS if v != executor]
        if len(others) != 1:
            sys.exit(
                "error: cannot default --reviewer unambiguously (more than two vendors known); "
                "pass --reviewer explicitly"
            )
        reviewer = others[0]
    elif reviewer not in VENDORS:
        sys.exit(f"error: --reviewer must be one of {VENDORS}")
    return executor, reviewer


def build_report(outcome: str, round_num: int, log_path: Path) -> str:
    lines = [
        f"Executor/Reviewer loop ended: {outcome}",
        f"Rounds run: {round_num}",
        f"Full session log (delete after reading): {log_path}",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--task-file", type=Path, required=True)
    parser.add_argument("--executor", choices=VENDORS)
    parser.add_argument("--reviewer", choices=VENDORS)
    parser.add_argument("--rounds", type=int, default=5)
    parser.add_argument(
        "--keep-log", action="store_true", help="Do not delete the scratch log file (debugging)."
    )
    args = parser.parse_args()

    executor, reviewer = resolve_vendors(args)
    root = args.root.resolve()
    task = args.task_file.read_text(encoding="utf-8")

    fd, log_name = tempfile.mkstemp(prefix="ab-executor-reviewer-", suffix=".md")
    os.close(fd)
    log_path = Path(log_name)
    log_path.write_text(f"# Task\n\n{task.strip()}\n", encoding="utf-8")

    seen_fingerprints_by_round: list[set[str]] = []
    outcome = "cap_exhausted"
    round_num = 0

    for round_num in range(1, args.rounds + 1):
        exec_prompt = build_prompt("executor", task, log_path.read_text(encoding="utf-8"), round_num)
        exec_output, exec_ok = launch(executor, "executor", root, exec_prompt)
        append_round(log_path, round_num, "Executor", exec_output)
        if not exec_ok:
            outcome = "vendor_invocation_failed"
            break

        rev_prompt = build_prompt("reviewer", task, log_path.read_text(encoding="utf-8"), round_num)
        rev_output, rev_ok = launch(reviewer, "reviewer", root, rev_prompt)
        append_round(log_path, round_num, "Reviewer Findings", rev_output)
        if not rev_ok:
            outcome = "vendor_invocation_failed"
            break

        findings = parse_findings(rev_output)
        if findings is None:
            outcome = "malformed_reviewer_output"
            break
        if not findings:
            outcome = "converged"
            break

        current_fps = {fingerprint(f) for f in findings}
        oscillating = set()
        for older_fps in seen_fingerprints_by_round[:-1]:  # two-or-more-rounds-back only
            oscillating |= current_fps & older_fps
        seen_fingerprints_by_round.append(current_fps)
        if len(oscillating) >= 2:
            outcome = "oscillating"
            break
    else:
        outcome = "cap_exhausted"

    print(build_report(outcome, round_num, log_path))
    if not args.keep_log:
        log_path.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
