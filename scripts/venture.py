#!/usr/bin/env python3
"""Deterministic Venture context and repository-to-wiki mapping primitives."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import tempfile
import time
from typing import Any


SUMMARY_START = "<!-- AGENT-BASE-VENTURE-SUMMARY:START -->"
SUMMARY_END = "<!-- AGENT-BASE-VENTURE-SUMMARY:END -->"
# Bootstrap anchors, tried in order, used only until the managed markers exist. The template
# heading comes first so a pristine AGENTS.md keeps its familiar placement, but AGENTS.md is a
# customize-once file and a repository may legitimately rename or drop that heading. The rules
# marker is the fallback because apply-agent-base.sh injects it into every downstream AGENTS.md.
SUMMARY_ANCHORS = ("## Priority Model", "<!-- AGENT-BASE-RULES:START -->")
SUMMARY_HEADING = "## Venture"
IDENTITY_SECTION = "Identity"
# The block is assembled from canonical sections, never from a second hand-authored artifact.
# It used to render one `## Summary` teaser, which measurably contained nothing but Identity plus
# a compressed Strategy; once Identity became a section of its own the teaser held nothing at all.
# Ordering is by decision value, not by hierarchy: Strategy states refusals and settles arguments,
# Goals name what a change serves, and Purpose and Vision are reached through the trigger because
# neither decides much on its own.
# The trailer says where the rest lives and nothing else. The imperative to judge a change and
# name its Goal belongs to the pointer printed last, so it is said once, in the position where
# being last is the whole point, rather than twice in one session.
SUMMARY_TRIGGER = (
    "Purpose and Vision: `.agents/VENTURE.md`. Key results: `.agents/goals/`."
)
# Every other line of the block is labelled, so an unlabelled first line reads as identity even
# when it is a stand-in. A substitute must admit it inline: the quality condition that reports a
# missing Identity prints hundreds of lines away and is explicitly deferrable, which is not the
# same as the block being honest about what it is showing.
IDENTITY_LABEL = "Identity:"
IDENTITY_FALLBACK_LABELS = {
    "summary": "Identity (unwritten; showing the retired ## Summary):",
    "purpose": "Identity (unwritten; showing ## Purpose):",
}
IDENTITY_RECOMMENDED_MAX_WORDS = 25
CONTEXT_MAX_WORDS = 250
DEFINED_SECTIONS = ("Purpose", "Vision", "Strategy")
MIRRORED_CATEGORIES = {
    "departments",
    "feedback",
    "goals",
    "products",
    "resources",
    "skills",
    "standards",
    "workflows",
}
CATALOG_NAMES = {
    "_DEPARTMENTS_CATALOG.md",
    "_FEEDBACK_CATALOG.md",
    "_GOALS_CATALOG.md",
    "_PRODUCTS_CATALOG.md",
    "_RESOURCES_CATALOG.md",
    "_ROADMAP_CATALOG.md",
    "_SKILLS_CATALOG.md",
    "_STANDARDS_CATALOG.md",
    "_WORKFLOW_CATALOG.md",
}
HEX_HASH = re.compile(r"^[0-9a-f]{64}$")
FRAMEWORK_PATHS = {"skills/permission-promote.md"}
CHECK_STAMPS = {
    "wiki": ".venture-wiki-last-check",
    "review": ".venture-review-last-check",
}
CHECK_TTLS = {
    "wiki": 86_400,
    "review": 30 * 86_400,
}


class VentureError(ValueError):
    """Invalid or incomplete Venture state."""


def word_count(text: str) -> int:
    return len(text.split())


def normalize_markdown(text: str) -> str:
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    return "\n".join(line.rstrip() for line in lines).rstrip() + "\n"


def content_hash(text: str) -> str:
    return hashlib.sha256(normalize_markdown(text).encode("utf-8")).hexdigest()


def _strip_comments(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def _section_value(text: str, heading: str) -> str | None:
    match = re.search(
        rf"^## {re.escape(heading)}\s*$\n(.*?)(?=^## |\Z)", text, flags=re.M | re.S
    )
    if not match:
        return None
    body = _strip_comments(match.group(1)).strip()
    if not body or "NOT YET DEFINED" in body:
        return None
    paragraphs = [part.strip() for part in re.split(r"\n\s*\n", body) if part.strip()]
    if not paragraphs:
        return None
    return " ".join(" ".join(paragraph.split()) for paragraph in paragraphs)


def load_venture(root: Path) -> dict[str, str]:
    path = root / ".agents" / "VENTURE.md"
    if not path.is_file():
        raise VentureError(".agents/VENTURE.md is missing")
    text = path.read_text(encoding="utf-8")
    values = {heading: _section_value(text, heading) for heading in DEFINED_SECTIONS}
    missing = [heading for heading, value in values.items() if value is None]
    if missing:
        raise VentureError("unfilled Venture section(s): " + ", ".join(missing))
    return {heading: value or "" for heading, value in values.items()}


def load_goals(root: Path) -> list[tuple[str, str]]:
    path = root / ".agents" / "goals" / "_GOALS_CATALOG.md"
    if not path.is_file():
        raise VentureError(".agents/goals/_GOALS_CATALOG.md is missing")
    text = path.read_text(encoding="utf-8")
    if "NOT YET DEFINED" in text:
        raise VentureError("Goals are not yet defined")
    goals: list[tuple[str, str]] = []
    for line in text.splitlines():
        match = re.match(r"^\|\s*([0-9]{3})\s*\|\s*(.*?)\s*\|", line)
        if not match:
            continue
        objective = re.sub(r"\[([^]]+)\]\([^)]*\)", r"\1", match.group(2)).strip()
        if objective and "TODO" not in objective and "<!--" not in objective:
            goals.append((match.group(1), " ".join(objective.split())))
    if not goals:
        raise VentureError("Goals contain no Objective lines")
    return goals


def venture_state(root: Path) -> tuple[dict[str, str], list[tuple[str, str]]]:
    return load_venture(root), load_goals(root)


def section_value(root: Path, heading: str) -> str:
    """One `.agents/VENTURE.md` section as the renderer sees it, or empty.

    Exposed so the guard reads sections through this extractor instead of its own. Two extractors
    are two chances to disagree, and they did: a shell strip of `<!-- ... -->` handled only the
    line that opened the comment, so a multi-line comment counted as content and the guard called
    a teaser over budget while the renderer, correctly, did not.
    """
    path = root / ".agents" / "VENTURE.md"
    if not path.is_file():
        return ""
    return _section_value(path.read_text(encoding="utf-8"), heading) or ""


def load_identity(root: Path) -> str | None:
    """The `## Identity` section, or None when a repository predates it.

    Deliberately not in DEFINED_SECTIONS. Identity became a first-class section after clients
    already existed, and `VENTURE.md` is customize-once, so it cannot simply appear. A missing
    Identity falls back to Purpose and is surfaced as a quality condition, never as a failure.
    """
    path = root / ".agents" / "VENTURE.md"
    if not path.is_file():
        return None
    return _section_value(path.read_text(encoding="utf-8"), IDENTITY_SECTION)


def _goals_line(goals: list[tuple[str, str]]) -> str:
    """Render every Objective in full; project-authored context owns its own budget."""
    return "; ".join(f"{identifier} {objective}" for identifier, objective in goals)


def render_summary(root: Path) -> str:
    try:
        venture, goals = venture_state(root)
    except VentureError as error:
        body = (
            "## Venture Summary\n\n"
            f"**Venture Context unavailable:** {error}. Define the canonical sources before "
            "substantive work. Full sources: `.agents/VENTURE.md` and "
            "`.agents/goals/_GOALS_CATALOG.md`."
        )
    else:
        # Ordered by what actually decides things, not by where each sits in the hierarchy.
        # Identity first because every line under it assumes you know what the thing is; Strategy
        # next because refusals settle arguments between options; Goals because that is the test
        # applied to every change. Purpose and Vision are deliberately absent: broad by design and
        # an aspiration respectively, neither settles a choice, and leaving them out is what pays
        # for carrying Strategy here at all.
        # Preference order, best available first. While a repository still carries the retired
        # `## Summary`, it is real authored context and a better stand-in than Purpose, which
        # answers a different question. Preserve it in full until the repository defines Identity.
        identity = load_identity(root)
        label = IDENTITY_LABEL
        if not identity:
            legacy = _section_value(
                (root / ".agents" / "VENTURE.md").read_text(encoding="utf-8"), "Summary"
            )
            if legacy:
                identity = legacy
                label = IDENTITY_FALLBACK_LABELS["summary"]
            else:
                identity = venture["Purpose"]
                label = IDENTITY_FALLBACK_LABELS["purpose"]
        body = "\n".join(
            [
                SUMMARY_HEADING,
                "",
                f"{label} {identity}",
                f"Strategy: {venture['Strategy']}",
                f"Goals: {_goals_line(goals)}",
                "",
                SUMMARY_TRIGGER,
            ]
        )
    return f"{SUMMARY_START}\n{body}\n{SUMMARY_END}"


def render_context(root: Path) -> str:
    """A pointer, printed last, not a second copy of the Venture.

    This used to restate Purpose, Vision, Strategy and every Goal, which duplicated most of the
    `AGENTS.md` block for no gain in reach: whenever the guard prints this it has already printed
    `AGENTS.md` a few lines above, so anything here arrives only in situations where the block
    arrived too. What the position does buy is recency, being the last thing in the session's
    context, and that is worth keeping at the price of a pointer rather than a duplicate.

    It still calls `venture_state` so `AB-VENTURE-001`'s stop-on-failure keeps working: an
    undefined Venture must fail here exactly as before.
    """
    venture_state(root)
    return "\n".join(
        [
            "# Venture",
            "",
            "Judge every substantive change against the `## Venture` block in `AGENTS.md`"
            " above, and name the Goal it serves.",
        ]
    )


def atomic_write(path: Path, content: str) -> None:
    mode = path.stat().st_mode & 0o777 if path.exists() else 0o644
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8") as handle:
            handle.write(content)
        os.chmod(temporary, mode)
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def refresh_summary(root: Path) -> bool:
    agents = root / "AGENTS.md"
    if not agents.is_file():
        raise VentureError("AGENTS.md is missing")
    current = agents.read_text(encoding="utf-8")
    block = render_summary(root)
    if SUMMARY_START in current or SUMMARY_END in current:
        if current.count(SUMMARY_START) != 1 or current.count(SUMMARY_END) != 1:
            raise VentureError("Venture Summary managed block is malformed")
        updated = re.sub(
            rf"{re.escape(SUMMARY_START)}.*?{re.escape(SUMMARY_END)}",
            lambda _: block,
            current,
            flags=re.S,
        )
    else:
        for anchor in SUMMARY_ANCHORS:
            if anchor in current:
                updated = current.replace(anchor, f"{block}\n\n{anchor}", 1)
                break
        else:
            raise VentureError(
                "AGENTS.md has no Venture Summary insertion anchor; expected one of "
                + " or ".join(SUMMARY_ANCHORS)
            )
    if updated == current:
        return False
    atomic_write(agents, updated)
    return True


def slug(segment: str) -> str:
    value = re.sub(r"\.md$", "", segment, flags=re.I)
    value = re.sub(r"[^A-Za-z0-9]+", "-", value).strip("-").lower()
    if not value:
        raise VentureError(f"cannot derive wiki path from {segment!r}")
    return value


def map_repository_path(relative: str) -> str | None:
    path = PurePosixPath(relative)
    parts = list(path.parts)
    if parts and parts[0] == ".agents":
        parts.pop(0)
    if not parts or any(part in {"", ".", ".."} for part in parts):
        raise VentureError("repository path must be a safe relative path")
    if parts[0] in {"agent-base", "sessions", "features"}:
        return None
    if "/".join(parts) in FRAMEWORK_PATHS:
        return None
    if parts[0].startswith(".") or parts[0] == "WIKI_SYNC.json":
        return None
    if parts == ["VENTURE.md"]:
        return ""
    if parts[0] not in MIRRORED_CATEGORIES:
        return None
    if any("TEMPLATE" in part.upper() for part in parts):
        return None
    if parts[-1] in CATALOG_NAMES:
        return slug(parts[0])
    if parts[-1].lower() in {"skill.md", "product.md"}:
        parts.pop()
    else:
        parts[-1] = re.sub(r"\.md$", "", parts[-1], flags=re.I)
    return "/".join(slug(part) for part in parts)


def inventory(root: Path) -> dict[str, Any]:
    agents = root / ".agents"
    documents: list[dict[str, str]] = []
    wiki_owners: dict[str, str] = {}
    if not agents.is_dir():
        raise VentureError(".agents directory is missing")
    candidates: list[tuple[Path, str, str, str]] = []
    for path in sorted(agents.rglob("*.md")):
        relative = path.relative_to(agents).as_posix()
        wiki_path = map_repository_path(relative)
        if wiki_path is None:
            continue
        text = path.read_text(encoding="utf-8")
        if relative == "VENTURE.md":
            try:
                load_venture(root)
            except VentureError:
                continue
        candidates.append((path, relative, wiki_path, text))

    active_categories = {
        PurePosixPath(relative).parts[0]
        for _, relative, _, _ in candidates
        if PurePosixPath(relative).name not in CATALOG_NAMES
        and relative != "VENTURE.md"
    }
    for path, relative, wiki_path, text in candidates:
        path_parts = PurePosixPath(relative).parts
        if path_parts[-1] in CATALOG_NAMES and path_parts[0] not in active_categories:
            continue
        if wiki_path in wiki_owners:
            raise VentureError(
                f"wiki path collision: {wiki_owners[wiki_path]} and {relative} map to "
                f"{wiki_path or '.'}"
            )
        wiki_owners[wiki_path] = relative
        title_match = re.search(r"^#\s+(.+?)\s*$", text, flags=re.M)
        documents.append(
            {
                "repository": relative,
                "wiki": wiki_path,
                "title": title_match.group(1) if title_match else path.stem,
                "hash": content_hash(text),
            }
        )
    legacy_features = agents / "features"
    return {
        "schema": 1,
        "legacy_flat_features": legacy_features.is_dir()
        and any(
            path.is_file() and not path.name.startswith("_")
            for path in legacy_features.glob("*.md")
        ),
        "documents": documents,
    }


def _safe_relative(value: Any, *, allow_empty: bool = False) -> bool:
    if not isinstance(value, str):
        return False
    if allow_empty and value == "":
        return True
    path = PurePosixPath(value)
    return bool(value) and not path.is_absolute() and ".." not in path.parts


def validate_manifest(path: Path) -> dict[str, Any]:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise VentureError(f"invalid sync manifest: {error}") from error
    if set(data) != {"schema", "last_successful_sync", "pages"} or data["schema"] != 1:
        raise VentureError("sync manifest has unsupported top-level schema")
    if not isinstance(data["last_successful_sync"], str) or not data["last_successful_sync"]:
        raise VentureError("sync manifest lacks last_successful_sync")
    if not isinstance(data["pages"], list):
        raise VentureError("sync manifest pages must be a list")
    seen_repositories: set[str] = set()
    seen_wiki_paths: set[str] = set()
    for page in data["pages"]:
        required = {"repository", "wiki", "repository_hash", "wiki_hash"}
        if not isinstance(page, dict) or set(page) != required:
            raise VentureError("sync manifest page has unsupported schema")
        if not _safe_relative(page["repository"]) or not _safe_relative(
            page["wiki"], allow_empty=True
        ):
            raise VentureError("sync manifest contains an unsafe relative path")
        if page["repository"] in seen_repositories:
            raise VentureError("sync manifest contains a duplicate repository path")
        if page["wiki"] in seen_wiki_paths:
            raise VentureError("sync manifest contains a duplicate wiki path")
        seen_repositories.add(page["repository"])
        seen_wiki_paths.add(page["wiki"])
        if not HEX_HASH.fullmatch(page["repository_hash"]) or not HEX_HASH.fullmatch(
            page["wiki_hash"]
        ):
            raise VentureError("sync manifest contains an invalid normalized hash")
    return data


def checked_path(root: Path, kind: str = "wiki") -> Path:
    return root / ".agents" / CHECK_STAMPS[kind]


def check_due(root: Path, ttl: int, kind: str = "wiki") -> bool:
    try:
        checked = int(checked_path(root, kind).read_text(encoding="utf-8").strip())
    except (OSError, ValueError):
        return True
    return time.time() - checked >= ttl


def wiki_config(root: Path, registry: Path | None = None) -> dict[str, str]:
    root = root.resolve()
    if registry is None:
        config_home = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
        registry = config_home / "agent-base" / "wikis.json"
        if not registry.exists():
            registry = config_home / "agent-base" / "downstreams.json"
    try:
        data = json.loads(registry.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise VentureError(f"wiki registry is unavailable or invalid: {error}") from error
    if not isinstance(data, dict):
        raise VentureError("wiki registry root must be an object")

    legacy = "wikis" not in data and "repositories" in data
    entries = data.get("repositories" if legacy else "wikis")
    if not isinstance(entries, list):
        raise VentureError("wiki registry wikis must be an array")

    matched: dict[str, Any] | None = None
    seen_paths: set[Path] = set()
    for item in entries:
        if not isinstance(item, dict) or not isinstance(item.get("path"), str):
            if legacy:
                continue
            raise VentureError("every wiki registry entry must have an absolute path")
        item_path = Path(item["path"]).expanduser()
        if not item_path.is_absolute():
            if legacy:
                continue
            raise VentureError("every wiki registry entry must have an absolute path")
        resolved_path = item_path.resolve()
        if resolved_path in seen_paths:
            raise VentureError(f"duplicate wiki registry path: {resolved_path}")
        seen_paths.add(resolved_path)
        if resolved_path == root:
            matched = item
    if matched is None:
        raise VentureError("this Venture has no entry in the wiki registry")
    mapping = matched.get("wiki") if legacy else matched
    if not isinstance(mapping, dict):
        raise VentureError("this Venture has no wiki mapping")
    connector = mapping.get("connector")
    wiki_root = mapping.get("root")
    if not isinstance(connector, str) or not re.fullmatch(r"[A-Za-z0-9._-]+", connector):
        raise VentureError("wiki connector must be a non-empty local identifier")
    if not _safe_relative(wiki_root) or "://" in wiki_root:
        raise VentureError("wiki root must be a safe relative path below the connector host")
    result = {"connector": connector, "root": wiki_root.strip("/")}
    host = mapping.get("host")
    if host is not None:
        if not isinstance(host, str) or not re.fullmatch(r"[a-z0-9][a-z0-9.-]*", host):
            raise VentureError("wiki host must be a lowercase hostname")
        result["host"] = host
    return result


def wiki_visibility(
    root: Path, registry: Path | None = None, path: str | None = None
) -> dict[str, str | None]:
    if registry is None:
        config_home = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
        registry = config_home / "agent-base" / "wikis.json"
        if not registry.exists():
            registry = config_home / "agent-base" / "downstreams.json"
    mapping = wiki_config(root, registry)
    target_path = path if path is not None else mapping["root"]
    if not _safe_relative(target_path) or any(char in target_path for char in "?#%\\"):
        raise VentureError("wiki visibility path must be a safe host-relative path")
    host = mapping.get("host")
    result: dict[str, str | None] = {
        "host": host,
        "path": target_path,
        "visibility": "unknown",
    }
    if host is None:
        return result
    try:
        data = json.loads(registry.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        raise VentureError(f"wiki registry is unavailable or invalid: {error}") from error
    policies = data.get("visibility_policies")
    policy = policies.get(host) if isinstance(policies, dict) else None
    if not isinstance(policy, dict) or policy.get("default") not in ("private", "public"):
        return result
    private_segments = policy.get("private_path_segments", [])
    if not isinstance(private_segments, list) or not all(
        isinstance(segment, str) and re.fullmatch(r"[A-Za-z0-9._~-]+", segment)
        for segment in private_segments
    ):
        return result
    segments = target_path.strip("/").split("/")
    result["visibility"] = (
        "private" if any(segment in segments for segment in private_segments) else policy["default"]
    )
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("."))
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("context")
    subparsers.add_parser("summary")
    subparsers.add_parser("refresh-summary")
    section = subparsers.add_parser("section")
    section.add_argument("heading")
    subparsers.add_parser("identity-recommended-max")
    subparsers.add_parser("inventory")
    mapping = subparsers.add_parser("map-path")
    mapping.add_argument("path")
    manifest = subparsers.add_parser("validate-manifest")
    manifest.add_argument("path", type=Path, nargs="?")
    due = subparsers.add_parser("check-due")
    due.add_argument("--kind", choices=tuple(CHECK_STAMPS), default="wiki")
    due.add_argument("--ttl", type=int)
    checked = subparsers.add_parser("mark-checked")
    checked.add_argument("--kind", choices=tuple(CHECK_STAMPS), default="wiki")
    config = subparsers.add_parser("wiki-config")
    config.add_argument("--registry", type=Path)
    visibility = subparsers.add_parser("wiki-visibility")
    visibility.add_argument("--registry", type=Path)
    visibility.add_argument("--path")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    root = args.root.resolve()
    try:
        if args.command == "context":
            print(render_context(root))
        elif args.command == "summary":
            print(render_summary(root))
        elif args.command == "identity-recommended-max":
            print(IDENTITY_RECOMMENDED_MAX_WORDS)
        elif args.command == "section":
            print(section_value(root, args.heading))
        elif args.command == "refresh-summary":
            print("updated" if refresh_summary(root) else "unchanged")
        elif args.command == "inventory":
            print(json.dumps(inventory(root), indent=2, sort_keys=True))
        elif args.command == "map-path":
            mapped = map_repository_path(args.path)
            print("excluded" if mapped is None else mapped or ".")
        elif args.command == "validate-manifest":
            path = args.path or root / ".agents" / "WIKI_SYNC.json"
            validate_manifest(path)
            print("valid")
        elif args.command == "check-due":
            ttl = args.ttl if args.ttl is not None else CHECK_TTLS[args.kind]
            print("due" if check_due(root, ttl, args.kind) else "not-due")
        elif args.command == "mark-checked":
            atomic_write(checked_path(root, args.kind), f"{int(time.time())}\n")
            print("checked")
        elif args.command == "wiki-config":
            print(json.dumps(wiki_config(root, args.registry), sort_keys=True))
        elif args.command == "wiki-visibility":
            print(json.dumps(wiki_visibility(root, args.registry, args.path), sort_keys=True))
    except VentureError as error:
        print(f"Venture: {error}", file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
