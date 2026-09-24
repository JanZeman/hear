#!/usr/bin/env python3
"""Operator-local capability mapping for agent-base Venture Staffing (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Which model answers a capability is an operator's fact, not an architectural one, and its
    absence degrades to today's behaviour rather than to a guess.

Two consequences follow, and both are deliberate.

**The mapping lives outside the repository**, beside `vendor-support.json`, `downstreams.json`, and
`wikis.json`. Which models an operator can actually reach depends on subscriptions and prices that
change monthly, and `GEN-001` keeps concrete infrastructure out of this repository. A default
shipped here would be wrong within a release or two and would quietly teach every client the wrong
answer in the meantime.

**A missing configuration is not an error.** With no file, staffing resolves to the session already
running plus whatever native helper the vendor provides, which is exactly what agent-base did before
this feature existed. That is the backward-compatibility guarantee: a client receives these scripts
and nothing about its sessions changes until an operator decides otherwise.
"""

from __future__ import annotations

import json
import os
from pathlib import Path
from typing import Any, NamedTuple

CONFIG_VERSION = 1

# `tool` never appears in the mapping: no model answers it, which is the point of it.
MAPPED_TIERS = ("cheap", "standard", "strong", "expert")


class ConfigError(RuntimeError):
    """Every refusal here. All of them fail closed, and none of them guesses a model."""


class Placement(NamedTuple):
    vendor: str
    model: str
    effort: str | None


class Config(NamedTuple):
    present: bool
    vendors: dict[str, dict[str, Placement]]
    roles: dict[str, dict[str, Any]]
    limits: dict[str, int]
    preference: tuple[str, ...]
    # Whether this operator has actually demonstrated that a vendor's resume path restates an
    # invocation contract. False by default and for every unlisted vendor: resume is a claim about
    # a vendor's behaviour, and nobody's claim is taken on trust here.
    resume_verified: dict[str, bool]

    @property
    def parent_only(self) -> bool:
        return not self.present


def config_path() -> Path:
    override = os.environ.get("AB_STAFFING_CONFIG")
    if override:
        return Path(override).expanduser()
    root = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    return root / "agent-base" / "staffing.json"


EMPTY = Config(present=False, vendors={}, roles={}, limits={}, preference=(), resume_verified={})


def load(path: Path | None = None) -> Config:
    target = path or config_path()
    try:
        text = target.read_text(encoding="utf-8")
    except FileNotFoundError:
        return EMPTY
    except OSError as error:
        raise ConfigError(f"cannot read {target}: {error}") from error
    try:
        raw = json.loads(text)
    except json.JSONDecodeError as error:
        # A damaged configuration is not an absent one. Falling back to parent-only here would hide
        # a typo behind behaviour that looks deliberately conservative.
        raise ConfigError(f"{target} is not valid JSON: {error}") from error
    return parse(raw, source=target)


def parse(raw: dict[str, Any], source: Path | str = "<memory>") -> Config:
    if not isinstance(raw, dict):
        raise ConfigError(f"{source} must hold an object")
    version = raw.get("version")
    if version != CONFIG_VERSION:
        raise ConfigError(
            f"{source} declares version {version!r}; this agent-base understands {CONFIG_VERSION}"
        )

    vendors: dict[str, dict[str, Placement]] = {}
    for vendor, tiers in (raw.get("vendors") or {}).items():
        if not isinstance(tiers, dict):
            raise ConfigError(f"{source}: vendor {vendor!r} must map tiers to placements")
        unknown = set(tiers) - set(MAPPED_TIERS)
        if unknown:
            raise ConfigError(
                f"{source}: vendor {vendor!r} names unknown tiers {sorted(unknown)}; the mapped "
                f"tiers are {list(MAPPED_TIERS)} and `tool` is deliberately not among them"
            )
        placements = {}
        for tier, placement in tiers.items():
            if not isinstance(placement, dict) or not placement.get("model"):
                raise ConfigError(
                    f"{source}: {vendor}.{tier} needs a model; an empty tier would be resolved by "
                    "guessing at a neighbouring one, and a guessed model is a guessed bill"
                )
            effort = placement.get("effort")
            if effort is not None and not isinstance(effort, str):
                raise ConfigError(f"{source}: {vendor}.{tier} effort must be a string or null")
            placements[tier] = Placement(vendor=vendor, model=placement["model"], effort=effort)
        vendors[vendor] = placements

    limits = raw.get("limits") or {}
    for key, value in limits.items():
        if not isinstance(value, int) or value <= 0:
            raise ConfigError(f"{source}: limit {key!r} must be a positive integer")

    preference = tuple(raw.get("preference") or ())
    if len(set(preference)) != len(preference):
        raise ConfigError(f"{source}: preference order repeats an entry")

    resume_verified = {
        vendor: bool(value) for vendor, value in (raw.get("resume_verified") or {}).items()
    }
    return Config(present=True, vendors=vendors, roles=raw.get("roles") or {}, limits=limits,
                  preference=preference, resume_verified=resume_verified)


def placement(config: Config, vendor: str, capability: str) -> Placement:
    """Which model and effort answer this capability for this vendor, or a specific refusal.

    Never substitutes a neighbouring tier. An unmapped tier is an operator decision that has not
    been made, and silently spending a tier up or down is precisely the behaviour this whole feature
    exists to stop being accidental.
    """
    if not config.present:
        raise ConfigError(
            "no staffing configuration: this session and its vendor's native helper are the only "
            f"Actors available, so capability {capability!r} cannot be staffed separately"
        )
    if vendor not in config.vendors:
        raise ConfigError(
            f"vendor {vendor!r} is not mapped; mapped vendors are {sorted(config.vendors)}"
        )
    tiers = config.vendors[vendor]
    if capability not in tiers:
        raise ConfigError(
            f"{vendor} has no model mapped for capability {capability!r}; map it or route this work "
            "to the human rather than spending a neighbouring tier"
        )
    return tiers[capability]


def available_capabilities(config: Config, vendor: str) -> dict[str, Placement]:
    """What routing may choose from. Empty without a configuration, which routing handles."""
    return dict(config.vendors.get(vendor, {}))


def resume_is_verified(config: Config, vendor: str) -> bool:
    return bool(config.resume_verified.get(vendor, False))
