#!/usr/bin/env python3
"""Safely prune one explicitly linked session-snapshot lineage."""

from __future__ import annotations

import argparse
from collections import defaultdict
from pathlib import Path
import re
import sys


CONTINUES = re.compile(r"^\*\*Continues\*\*: `([^`\n]+)`\s*$", re.MULTILINE)
CONTINUES_PREFIX = re.compile(r"^\*\*Continues\*\*:", re.MULTILINE)


class PruneRefused(RuntimeError):
    """The requested lineage cannot be proved safe to prune."""


def predecessor(path: Path) -> str | None:
    text = path.read_text(encoding="utf-8")
    matches = CONTINUES.findall(text)
    declarations = CONTINUES_PREFIX.findall(text)
    if len(matches) != len(declarations) or len(matches) > 1:
        raise PruneRefused(f"invalid Continues metadata in {path.name}")
    return matches[0] if matches else None


def safe_name(value: str) -> str:
    candidate = Path(value)
    if (
        not value.endswith(".md")
        or candidate.name != value
        or value in {".", ".."}
        or "/" in value
        or "\\" in value
    ):
        raise PruneRefused(f"unsafe predecessor path: {value!r}")
    return value


def validate_start(path: Path) -> tuple[Path, Path]:
    supplied = path.absolute()
    if supplied.is_symlink() or not supplied.is_file() or supplied.suffix != ".md":
        raise PruneRefused("the current snapshot must be a regular Markdown file")
    directory = supplied.parent
    if directory.is_symlink() or directory.parent.is_symlink():
        raise PruneRefused("the session directory must not be a symbolic link")
    if directory.name != "sessions" or directory.parent.name != ".agents":
        raise PruneRefused("the snapshot must be directly under .agents/sessions")
    return supplied, directory


def existing_chain(current: Path) -> tuple[list[Path], Path]:
    start, directory = validate_start(current)
    chain: list[Path] = []
    seen: set[str] = set()
    node = start

    while True:
        if node.name in seen:
            raise PruneRefused(f"cycle detected at {node.name}")
        seen.add(node.name)
        chain.append(node)
        prior = predecessor(node)
        if prior is None:
            break
        prior = safe_name(prior)
        candidate = directory / prior
        if not candidate.exists():
            # A previously pruned predecessor may form the boundary behind the
            # existing chain. It is never itself inferred, grouped, or deleted.
            break
        if candidate.is_symlink() or not candidate.is_file():
            raise PruneRefused(f"predecessor is not a regular file: {prior}")
        node = candidate

    return chain, directory


def child_index(directory: Path) -> dict[str, set[str]]:
    children: dict[str, set[str]] = defaultdict(set)
    for path in directory.glob("*.md"):
        if path.is_symlink() or not path.is_file():
            continue
        try:
            prior = predecessor(path)
        except (OSError, UnicodeError, PruneRefused):
            # Unreliable legacy metadata does not establish lineage.
            continue
        if prior is None:
            continue
        try:
            prior = safe_name(prior)
        except PruneRefused:
            continue
        children[prior].add(path.name)
    return children


def prune(current: Path, *, dry_run: bool = False) -> list[Path]:
    chain, directory = existing_chain(current)
    children = child_index(directory)

    if children.get(chain[0].name):
        raise PruneRefused(f"{chain[0].name} is not the lineage tip")
    for node in chain:
        if len(children.get(node.name, set())) > 1:
            raise PruneRefused(f"lineage branches at {node.name}")

    victims = chain[2:]
    if not dry_run:
        for path in victims:
            path.unlink()
    return victims


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("snapshot", type=Path, help="newest snapshot in .agents/sessions")
    parser.add_argument("--dry-run", action="store_true", help="report without deleting")
    args = parser.parse_args(argv)

    try:
        victims = prune(args.snapshot, dry_run=args.dry_run)
    except (OSError, UnicodeError, PruneRefused) as exc:
        print(f"Session snapshot pruning refused: {exc}", file=sys.stderr)
        return 2

    action = "Would prune" if args.dry_run else "Pruned"
    if victims:
        print(f"{action}: " + ", ".join(path.name for path in victims))
    else:
        print("Pruned: nothing; the lineage already retains at most two snapshots.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
