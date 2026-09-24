#!/usr/bin/env python3
"""Report the complete always-on context delivered at session start."""

from __future__ import annotations

import argparse
from pathlib import Path
import re


ANSI_ESCAPE = re.compile(r"\x1b\[[0-9;]*m")
DEFAULT_LIMIT = 5000
GAUGE_WIDTH = 20


def word_count(text: str) -> int:
    return len(ANSI_ESCAPE.sub("", text).split())


def render_report(vendor: str, primary_name: str, primary_words: int, guard_words: int) -> list[str]:
    base_words = primary_words + guard_words
    total = base_words
    while True:
        percentage = round(total * 100 / DEFAULT_LIMIT)
        filled = min(GAUGE_WIDTH, round(GAUGE_WIDTH * total / DEFAULT_LIMIT))
        report_words = total - base_words
        lines = [
            f"--- Always-on context ({vendor.title()}) ---",
            f"[{'#' * filled}{'-' * (GAUGE_WIDTH - filled)}] "
            f"{total:,} / {DEFAULT_LIMIT:,} words ({percentage}%)",
            f"  {primary_name}: {primary_words:,}",
            f"  Guard output: {guard_words:,}",
            f"  This report: {report_words:,}",
        ]
        if total > DEFAULT_LIMIT:
            lines.append(
                "  WARNING: Combined context is over budget. Preserve diagnostics and use "
                "Context Budgeting before expanding always-on content."
            )
        lines.append("")
        measured = base_words + word_count("\n".join(lines))
        if measured == total:
            return lines
        total = measured


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--vendor", choices=("claude", "codex", "warp", "copilot"), required=True)
    parser.add_argument("--guard-output", type=Path, required=True)
    args = parser.parse_args()

    root = args.root.resolve()
    guard_words = word_count(args.guard_output.read_text(encoding="utf-8"))
    # claude/warp/copilot each have their own adapter file natively read by their tool, separate
    # from the guard's stdout; codex has no such adapter and reads AGENTS.md itself natively
    # instead (the guard's own AGENT-BASE-AGENTS-MD-PRINT block skips forcing it for codex only).
    vendor_primary = {
        "claude": ("CLAUDE.md adapter", root / "CLAUDE.md"),
        "warp": ("WARP.md adapter", root / "WARP.md"),
        "copilot": ("Copilot instructions adapter", root / ".github/copilot-instructions.md"),
    }
    primary_name, primary_path = vendor_primary.get(
        args.vendor, ("Native AGENTS.md", root / "AGENTS.md")
    )
    primary_words = word_count(primary_path.read_text(encoding="utf-8"))

    print("\n".join(render_report(args.vendor, primary_name, primary_words, guard_words)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
