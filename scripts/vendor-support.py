#!/usr/bin/env python3
"""Validate and display the operator-local Agent Base vendor support profile."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import sys


PROFILE_VERSION = 1
VENDORS = ("claude", "codex", "copilot", "warp")
TIERS = ("primary", "maintained", "compatibility-only")
TIER_RANK = {tier: index for index, tier in enumerate(TIERS)}


def profile_path() -> Path:
    override = os.environ.get("AB_VENDOR_SUPPORT_CONFIG")
    if override:
        return Path(override).expanduser()
    home = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    return home / "agent-base" / "vendor-support.json"


def load_profile(path: Path) -> dict:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ValueError(
            f"profile is missing: {path}; create it from vendor-support.md"
        ) from exc
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"cannot read {path}: {exc}") from exc
    if not isinstance(data, dict):
        raise ValueError(f"profile must be a JSON object: {path}")
    return data


def validate(profile: dict) -> None:
    if profile.get("version") != PROFILE_VERSION:
        raise ValueError(f"version must be {PROFILE_VERSION}")
    priority = profile.get("priority")
    if not isinstance(priority, list) or not all(isinstance(value, str) for value in priority):
        raise ValueError("priority must be a list of vendor names")
    if len(priority) != len(set(priority)) or set(priority) != set(VENDORS):
        raise ValueError(f"priority must name each supported vendor exactly once: {', '.join(VENDORS)}")
    support = profile.get("support")
    if not isinstance(support, dict) or set(support) != set(VENDORS):
        raise ValueError(f"support must classify each supported vendor: {', '.join(VENDORS)}")
    if any(tier not in TIERS for tier in support.values()):
        raise ValueError(f"support tiers must be one of: {', '.join(TIERS)}")
    if "primary" not in support.values():
        raise ValueError("support must include at least one Primary vendor")
    ranks = [TIER_RANK[support[vendor]] for vendor in priority]
    if ranks != sorted(ranks):
        raise ValueError("priority must list Primary, then Maintained, then Compatibility-only vendors")


def render(profile: dict, path: Path) -> str:
    support = profile["support"]
    lines = [f"Vendor support profile: {path}", f"Priority order: {', '.join(profile['priority'])}"]
    for tier in TIERS:
        names = [vendor for vendor in profile["priority"] if support[vendor] == tier]
        label = {"primary": "Primary", "maintained": "Maintained", "compatibility-only": "Compatibility-only"}[tier]
        lines.append(f"{label}: {', '.join(names) if names else '(none)'}")
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("check", "status"), nargs="?", default="status")
    args = parser.parse_args(argv)
    path = profile_path()
    try:
        profile = load_profile(path)
        validate(profile)
    except ValueError as exc:
        print(f"Vendor support profile error: {exc}", file=sys.stderr)
        return 1
    print(render(profile, path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
