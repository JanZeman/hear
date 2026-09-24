#!/usr/bin/env python3
"""The operator-local record of which model sits on which rung (roadmap item 158).

The predicate this module enforces, stated before any code reads it:

    Which model is expensive is a recorded fact with a date on it, not something each agent works
    out from whatever it happens to know.

Agent-base has had a capability ladder since item 148 and has never had the answer the ladder
depends on: which of a vendor's models answers `cheap`, and which one is the one you do not want a
two-line repair running on. Every agent estimated it, and an estimate from a model's own training
goes stale the month a vendor changes its lineup.

**It lives outside the repository**, beside `vendor-support.json`, `downstreams.json`, and
`staffing.json`. Model names and prices change monthly and depend on the operator's subscription, so
`GEN-001` keeps them out of here and a shipped default would teach every client a wrong answer that
survives until the next release.

**Cost is recorded as a ratio, not a price.** What an operator pays depends on a plan that may not
bill per token at all, and on a subscription what is consumed is quota rather than money. A currency
figure would be wrong for almost everyone reading it. What stays true long enough to be worth
recording is that one model burns roughly so many times another.

**Effort is a second dimension and may be the larger one.** A rung says what a model charges per
token and says nothing about how many tokens the work produces, and reasoning tokens are output
tokens. High effort on a cheap model can cost more than low effort on an expensive one, so the two
multiply and neither answers the question alone. An effort observation belongs to the MODEL it
measured, with a sample count. An unmeasured MODELMODE has no inferred multiplier.

**A stale ladder says it is stale.** It carries the date it was last reviewed and how long a review
is good for. An out-of-date answer presented confidently is worse than no answer, because nobody
goes looking for it.
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import sys

LADDER_VERSION = 2

# The same rungs `staffing_routing` uses, deliberately. Two vocabularies for one idea would drift
# within a release, and this file exists to be the single place the question is answered.
RUNGS = ("cheap", "standard", "strong", "expert")
RUNG_RANK = {rung: index for index, rung in enumerate(RUNGS)}

# The rungs a session should announce before it starts spending. Not a refusal: an expensive model
# is the right choice often enough that stopping the work would be worse than the cost.
EXPENSIVE_FROM = "strong"

DEFAULT_REVIEW_DAYS = 90

# Unknown until an operator measures it. 1 everywhere means "effort is not accounted for here",
# which is honest; a made-up curve would look like knowledge.
DEFAULT_EFFORT_MULTIPLIER = 1.0


class LadderError(ValueError):
    """Every refusal here. All of them fail closed, and none of them guesses a rung."""


def ladder_path() -> Path:
    override = os.environ.get("AB_MODEL_LADDER_CONFIG")
    if override:
        return Path(override).expanduser()
    home = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))
    return home / "agent-base" / "model-ladder.json"


def load(path: Path | None = None) -> dict:
    target = path or ladder_path()
    try:
        raw = target.read_text(encoding="utf-8")
    except FileNotFoundError as error:
        raise LadderError(
            f"no model ladder at {target}; without it nothing here knows which model is expensive"
        ) from error
    except OSError as error:
        raise LadderError(f"cannot read {target}: {error}") from error
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as error:
        raise LadderError(f"{target} is not valid JSON: {error}") from error
    validate(data)
    return data


def validate(data: dict) -> None:
    if not isinstance(data, dict):
        raise LadderError("the ladder must be a JSON object")
    if data.get("version") != LADDER_VERSION:
        raise LadderError(f"version must be {LADDER_VERSION}")
    reviewed = data.get("reviewed")
    if not isinstance(reviewed, str) or not _date(reviewed):
        raise LadderError("reviewed must be a YYYY-MM-DD date; a ladder with no date cannot go stale")
    days = data.get("review_after_days", DEFAULT_REVIEW_DAYS)
    if not isinstance(days, int) or days <= 0:
        raise LadderError("review_after_days must be a positive whole number of days")
    vendors = data.get("vendors")
    if not isinstance(vendors, dict) or not vendors:
        raise LadderError("vendors must be a non-empty object keyed by vendor name")
    for vendor, models in vendors.items():
        if not isinstance(models, dict) or not models:
            raise LadderError(f"{vendor} names no models")
        for model, facts in models.items():
            if not isinstance(facts, dict):
                raise LadderError(f"{vendor}/{model} must be an object")
            rung = facts.get("rung")
            if rung not in RUNGS:
                raise LadderError(
                    f"{vendor}/{model} has rung {rung!r}; the rungs are {list(RUNGS)}"
                )
            names = facts.get("aliases", [])
            if not isinstance(names, list) or any(not isinstance(one, str) for one in names):
                raise LadderError(f"{vendor}/{model} aliases must be a list of strings")
            cost = facts.get("relative_cost")
            if not isinstance(cost, (int, float)) or isinstance(cost, bool) or cost <= 0:
                raise LadderError(
                    f"{vendor}/{model} needs a positive relative_cost; it is a ratio against the "
                    "cheapest model recorded here, not a price"
                )
            efforts = facts.get("efforts", {})
            if not isinstance(efforts, dict):
                raise LadderError(f"{vendor}/{model} efforts must be an object")
            for effort, observation in efforts.items():
                if not isinstance(observation, dict):
                    raise LadderError(f"{vendor}/{model} effort {effort!r} must be an object")
                factor = observation.get("relative_burn")
                samples = observation.get("samples")
                if (not isinstance(factor, (int, float)) or isinstance(factor, bool)
                        or factor <= 0):
                    raise LadderError(f"{vendor}/{model} effort {effort!r} needs positive relative_burn")
                if not isinstance(samples, int) or samples < 5:
                    raise LadderError(f"{vendor}/{model} effort {effort!r} needs at least five samples")


def _date(value: str) -> dt.date | None:
    try:
        return dt.date.fromisoformat(value)
    except ValueError:
        return None


def staleness(data: dict, today: dt.date | None = None) -> str | None:
    """How overdue a review is, or None while it is current."""
    reviewed = _date(data["reviewed"])
    days = data.get("review_after_days", DEFAULT_REVIEW_DAYS)
    overdue = ((today or dt.date.today()) - reviewed).days - days
    if overdue <= 0:
        return None
    return (f"the model ladder was last reviewed on {data['reviewed']} and is {overdue} days past "
            f"its {days}-day review; vendors change their lineups, so treat its rungs as a guess "
            "until somebody checks them")


def _facts(data: dict, vendor: str, model: str) -> dict | None:
    """The record for this model, found by its full name or by an alias a CLI accepts.

    Aliases exist because the two files that name models are written for different readers. The
    ladder records what a vendor publishes, which is a full versioned identifier; the staffing map
    records what an operator types into a command line, which is often a short name the CLI resolves
    itself. Without this, the same model under its two names read as two models, one of them
    unrecorded, and the cross-check reported a disagreement that was only a spelling.
    """
    models = data.get("vendors", {}).get(vendor, {})
    facts = models.get(model)
    if isinstance(facts, dict):
        return facts
    for candidate in models.values():
        if isinstance(candidate, dict) and model in candidate.get("aliases", []):
            return candidate
    return None


def rung(data: dict, vendor: str, model: str) -> str | None:
    """The rung this model sits on, or None when the ladder has never heard of it.

    None is a real answer and is not the same as cheap. An unrecorded model is one nobody has
    classified, and the caller is expected to say so rather than assume the comfortable reading.
    """
    facts = _facts(data, vendor, model)
    return facts.get("rung") if facts else None


def relative_cost(data: dict, vendor: str, model: str) -> float | None:
    facts = _facts(data, vendor, model)
    return float(facts["relative_cost"]) if facts else None


def effort_multiplier(data: dict, vendor: str, model: str, effort: str | None) -> float | None:
    """A MODELMODE's measured burn multiplier, never a global guess."""
    if not effort:
        return DEFAULT_EFFORT_MULTIPLIER
    facts = _facts(data, vendor, model)
    if not facts:
        return None
    observation = facts.get("efforts", {}).get(effort)
    if not observation:
        return None
    return float(observation["relative_burn"])


def burn(data: dict, vendor: str, model: str, effort: str | None = None) -> float | None:
    """What this model at this effort costs, relative to the cheapest model at its default effort.

    None when the model is unrecorded. Multiplying two ratios is the whole arithmetic: the rung
    prices a token and the effort decides how many of them the work produces.
    """
    base = relative_cost(data, vendor, model)
    factor = effort_multiplier(data, vendor, model, effort)
    return None if base is None or factor is None else base * factor


def modelmodes(data: dict) -> list[dict]:
    """Known MODELMODE costs, ordered without inventing capability ranks."""
    rows = []
    for vendor, models in data["vendors"].items():
        for model, facts in models.items():
            rows.append({"vendor": vendor, "model": model, "effort": None,
                         "relative_burn": float(facts["relative_cost"]), "samples": None})
            for effort, observation in facts.get("efforts", {}).items():
                rows.append({"vendor": vendor, "model": model, "effort": effort,
                             "relative_burn": round(float(facts["relative_cost"])
                                                    * float(observation["relative_burn"]), 3),
                             "samples": observation["samples"]})
    return sorted(rows, key=lambda row: (row["relative_burn"], row["vendor"], row["model"],
                                         row["effort"] or ""))


def observed_capability(events: list[dict]) -> list[dict]:
    """Report verified outcomes by MODELMODE and required capability, not a fabricated rank."""
    grouped: dict[tuple[str, str, str | None, str], list[str]] = {}
    for event in events:
        if event.get("event") != "invocation_stop":
            continue
        vendor, model, capability = event.get("vendor"), event.get("model"), event.get("capability")
        if not vendor or not model or not capability:
            continue
        key = (vendor, model, event.get("effort"), capability)
        grouped.setdefault(key, []).append(str(event.get("outcome") or "unknown"))
    return [{"vendor": vendor, "model": model, "effort": effort, "capability": capability,
             "samples": len(outcomes), "succeeded": outcomes.count("succeeded"),
             "unverified": outcomes.count("unverified"), "failed": outcomes.count("failed")}
            for (vendor, model, effort, capability), outcomes in sorted(grouped.items())]


def migrate_v1(data: dict) -> dict:
    """Make v1 explicit without treating its global effort curve as measured truth."""
    if data.get("version") != 1:
        raise LadderError("only a version 1 ladder can be migrated")
    if data.get("effort_multipliers"):
        raise LadderError("v1 global effort_multipliers cannot become per-MODEL evidence; measure again")
    migrated = json.loads(json.dumps(data))
    migrated["version"] = LADDER_VERSION
    migrated.pop("effort_multipliers", None)
    for models in migrated["vendors"].values():
        for facts in models.values():
            facts["efforts"] = {}
    return migrated


def is_expensive(data: dict, vendor: str, model: str, effort: str | None = None) -> bool:
    """Whether a session on this model, at this effort, should announce itself.

    Either half can carry it. A model at or above the expensive rung qualifies whatever its effort,
    and a cheaper model qualifies once its effort pushes it past what the expensive rung costs at
    default effort. The second case is why effort is here at all.
    """
    found = rung(data, vendor, model)
    if found is None:
        return False
    if RUNG_RANK[found] >= RUNG_RANK[EXPENSIVE_FROM]:
        return True
    spent = burn(data, vendor, model, effort)
    floor = min((relative_cost(data, vendor, name)
                 for name, facts in data.get("vendors", {}).get(vendor, {}).items()
                 if RUNG_RANK[facts["rung"]] >= RUNG_RANK[EXPENSIVE_FROM]), default=None)
    return floor is not None and spent is not None and spent >= floor


def models_at_or_above(data: dict, floor: str = EXPENSIVE_FROM) -> dict[str, list[str]]:
    """Every model a session should announce, by vendor. What the session-start warning prints."""
    if floor not in RUNGS:
        raise LadderError(f"unknown rung {floor!r}; the rungs are {list(RUNGS)}")
    found: dict[str, list[str]] = {}
    for vendor, models in data.get("vendors", {}).items():
        named = sorted(name for name, facts in models.items()
                       if RUNG_RANK[facts["rung"]] >= RUNG_RANK[floor])
        if named:
            found[vendor] = named
    return found


def observed_efforts(events: list[dict], min_samples: int = 5) -> dict:
    """What ordinary work has shown about the cost of each reasoning effort.

    Built from staffing receipts rather than from sessions run to be measured, on the human's
    instruction and because it is the better experiment anyway: the sample is the work actually
    done, at its real sizes, instead of a benchmark chosen to be easy to compare.

    The weakness of that is worth stating rather than hiding. This is observational and uncontrolled:
    two efforts are compared across different work units, so a model given hard problems at high
    effort and easy ones at low effort will look more expensive at high effort than it is. What
    defends it is volume and the sample counts reported alongside every ratio. Treat a ratio with
    few samples as a rumour.

    Returns per model, because an effort may not cost the same on every model, and that is itself
    something worth finding out rather than assuming away.
    """
    # Two measures, kept apart because they answer different questions. Thinking tokens *detect*
    # the effect: a higher effort can think longer and answer more briefly, so a total can stay flat
    # while the work behind it changes. Total output is what is *billed*, so it is the only one a
    # cost multiplier may be built from. Measured live on one model: thinking moved 16.6x between
    # two efforts while the bill moved 3.8x, and reporting the first as a cost would have
    # overstated it more than four-fold.
    grouped: dict[tuple[str, str, str], dict[str, list[int]]] = {}
    for event in events:
        if event.get("event") != "invocation_stop":
            continue
        model, effort = event.get("model"), event.get("effort")
        billed, thinking = event.get("output_tokens"), event.get("thinking_tokens")
        if not event.get("usage_schema"):
            # Written before the token categories had defined meanings. Skipped rather than read,
            # because an ambiguous record that is counted anyway becomes calibrated data without
            # anyone deciding that it should.
            continue
        if not model or not effort or not isinstance(billed, (int, float)):
            continue
        bucket = grouped.setdefault((event.get("vendor", "unknown"), model, effort),
                                    {"billed": [], "thinking": []})
        bucket["billed"].append(int(billed))
        if isinstance(thinking, (int, float)):
            bucket["thinking"].append(int(thinking))

    per_model: dict[str, dict] = {}
    for (vendor, model, effort), samples in grouped.items():
        per_model.setdefault(f"{vendor}/{model}", {})[effort] = {
            "samples": len(samples["billed"]),
            "median_tokens": _median(samples["billed"]),
            "median_thinking": _median(samples["thinking"]) if samples["thinking"] else None,
        }

    report: dict[str, dict] = {}
    for name, efforts in sorted(per_model.items()):
        usable = {effort: facts for effort, facts in efforts.items()
                  if facts["samples"] >= min_samples}
        if len(usable) < 2:
            # One effort proves nothing about a ratio, and a thin sample is worse than none because
            # it looks like an answer.
            report[name] = {"efforts": efforts, "ratios": None, "thinking_ratios": None,
                            "why": f"needs two efforts with at least {min_samples} samples each"}
            continue
        base = min(usable.values(), key=lambda facts: facts["median_tokens"])
        thinking_base = min((facts["median_thinking"] for facts in usable.values()
                             if facts["median_thinking"]), default=None)
        report[name] = {
            "efforts": efforts,
            # The one a multiplier may be built from: what the vendor bills.
            "ratios": {effort: round(facts["median_tokens"] / base["median_tokens"], 2)
                       for effort, facts in sorted(usable.items())},
            # The one that shows whether the effort setting does anything at all.
            "thinking_ratios": ({effort: round((facts["median_thinking"] or 0) / thinking_base, 2)
                                 for effort, facts in sorted(usable.items())}
                                if thinking_base else None),
            "why": None,
        }
    return report


def _median(values: list[int]) -> float:
    ordered = sorted(values)
    middle = len(ordered) // 2
    if len(ordered) % 2:
        return float(ordered[middle])
    return (ordered[middle - 1] + ordered[middle]) / 2


def alternatives(data: dict, floor: str = EXPENSIVE_FROM) -> dict[str, list[dict]]:
    """For each expensive model, the cheapest one recorded for the same vendor, and the ratio.

    The ratio is a price ratio and nothing more. It does not claim the cheaper model could do the
    work, which is a judgement no ladder can make and which the reader has to make instead. Saying
    "about five times cheaper" is still worth more than "a cheaper one may be enough", because the
    reader who has to decide cannot do arithmetic on a warning that contains no numbers.
    """
    if floor not in RUNGS:
        raise LadderError(f"unknown rung {floor!r}; the rungs are {list(RUNGS)}")
    found: dict[str, list[dict]] = {}
    for vendor, models in data.get("vendors", {}).items():
        priced = {name: float(facts["relative_cost"]) for name, facts in models.items()}
        if not priced:
            continue
        cheapest = min(priced, key=lambda name: priced[name])
        rows = []
        for name, facts in sorted(models.items()):
            if RUNG_RANK[facts["rung"]] < RUNG_RANK[floor]:
                continue
            row = {"model": name, "rung": facts["rung"], "cost": priced[name],
                   "cheapest": cheapest, "cheapest_cost": priced[cheapest]}
            # A vendor whose only model is expensive has no alternative to offer, and saying "one
            # times cheaper" would be worse than saying nothing.
            row["ratio"] = (round(priced[name] / priced[cheapest], 1)
                            if cheapest != name and priced[cheapest] > 0 else None)
            rows.append(row)
        if rows:
            found[vendor] = rows
    return found


def disagreements(data: dict, staffing: dict) -> list[str]:
    """Where the operator's staffing choices contradict the ladder's facts.

    The two files answer different questions and are both needed. `staffing.json` records which
    model an operator *chose* for a capability; this file records which rung a model *is* on. They
    can disagree, and a disagreement is worth reporting rather than resolving automatically: only
    the operator knows which of the two is out of date.
    """
    found = []
    for vendor, tiers in (staffing.get("vendors") or {}).items():
        for tier, placement in (tiers or {}).items():
            model = (placement or {}).get("model")
            if not model:
                continue
            actual = rung(data, vendor, model)
            if actual is None:
                found.append(
                    f"staffing maps {vendor} {tier!r} to {model}, which the ladder does not list")
            elif actual != tier:
                found.append(
                    f"staffing maps {vendor} {tier!r} to {model}, which the ladder puts at "
                    f"{actual!r}")
    return sorted(found)


def _receipt_events() -> list[dict] | None:
    """Staffing receipts, or None when this operator keeps none. Never fails a command."""
    import importlib.util
    try:
        path = Path(__file__).resolve().with_name("route-receipts.py")
        spec = importlib.util.spec_from_file_location("route_receipts_probe", path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        events = module.events_path()
        if not events.exists():
            return None
        found = []
        for line in events.read_text(encoding="utf-8").splitlines():
            try:
                found.append(json.loads(line))
            except ValueError:
                continue
        return found
    except Exception:
        return None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Inspect the operator-local model ladder.")
    parser.add_argument("--path", type=Path, default=None)
    parser.add_argument("--rung", nargs=2, metavar=("VENDOR", "MODEL"),
                        help="print the rung and relative cost of one model")
    parser.add_argument("--expensive", action="store_true",
                        help="list the models a session should announce before spending")
    parser.add_argument("--check", action="store_true",
                        help="validate the ladder and report staleness")
    parser.add_argument("--efforts", action="store_true",
                        help="what ordinary work has shown about each effort's cost")
    parser.add_argument("--modelmodes", action="store_true",
                        help="known MODELMODE costs, ordered without a capability claim")
    parser.add_argument("--capability-evidence", action="store_true",
                        help="verified outcomes by MODELMODE and required capability")
    parser.add_argument("--migrate", action="store_true",
                        help="rewrite an empty-effort version 1 ladder as version 2")
    args = parser.parse_args(argv)

    try:
        target = args.path or ladder_path()
        raw = json.loads(target.read_text(encoding="utf-8"))
        if args.migrate:
            data = migrate_v1(raw)
            target.write_text(json.dumps(data, indent=2, sort_keys=True) + "\n", encoding="utf-8")
            print(f"migrated model ladder to version {LADDER_VERSION}: {target}")
            return 0
        data = load(args.path)
    except LadderError as error:
        print(f"model-ladder: {error}", file=sys.stderr)
        return 2

    if args.rung:
        vendor, model = args.rung
        found = rung(data, vendor, model)
        if found is None:
            print(f"{vendor} {model}: not recorded; classify it before routing by cost")
            return 1
        print(f"{vendor} {model}: {found}, about {relative_cost(data, vendor, model)}x the cheapest")
        return 0

    if args.efforts:
        events = _receipt_events()
        if events is None:
            print("no staffing receipts yet; effort costs are learned from ordinary work")
            return 1
        report = observed_efforts(events)
        if not report:
            print("no invocation reported both an effort and a token count yet")
            return 1
        for name, facts in report.items():
            print(name)
            for effort, seen in sorted(facts["efforts"].items()):
                print(f"  {effort}: {seen['samples']} runs, "
                      f"median {seen['median_tokens']:.0f} tokens")
            if facts["ratios"]:
                print(f"  billed ratios:   {facts['ratios']}")
                if facts["thinking_ratios"]:
                    print(f"  thinking ratios: {facts['thinking_ratios']}  (detection, not cost)")
            else:
                print(f"  no ratio: {facts['why']}")
        print("\nObservational, not controlled: these compare different work units. "
              "Few samples means a rumour.")
        return 0

    if args.modelmodes:
        for row in modelmodes(data):
            mode = f"{row['vendor']}/{row['model']}" + (f"/{row['effort']}" if row["effort"] else "")
            evidence = f", {row['samples']} samples" if row["samples"] is not None else ""
            print(f"{mode}: {row['relative_burn']}x{evidence}")
        return 0

    if args.capability_evidence:
        events = _receipt_events() or []
        for row in observed_capability(events):
            mode = f"{row['vendor']}/{row['model']}" + (f"/{row['effort']}" if row["effort"] else "")
            print(f"{mode} needs {row['capability']}: {row['succeeded']}/{row['samples']} succeeded, "
                  f"{row['unverified']} unverified, {row['failed']} failed")
        return 0

    if args.expensive:
        for vendor, rows in sorted(alternatives(data).items()):
            for row in rows:
                if row["ratio"]:
                    print(f"{vendor}: {row['model']} ({row['rung']}) is about "
                          f"{row['ratio']}x the cost of {row['cheapest']}")
                else:
                    print(f"{vendor}: {row['model']} ({row['rung']}), no cheaper model recorded")
        return 0

    stale = staleness(data)
    print(f"model ladder at {args.path or ladder_path()}: valid, reviewed {data['reviewed']}")
    if stale:
        print(f"note: {stale}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
