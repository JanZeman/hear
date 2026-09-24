#!/usr/bin/env python3
"""Invariants every vendor launch in this repository must satisfy (roadmap item 148).

The predicate this module enforces, stated before any code reads it:

    Whatever else a launcher chooses, an advisory role is never handed the means to act, and no role
    is ever handed a way around its sandbox.

Four places in this repository build a Claude or Codex command line: the two fleet launch gates, the
Executor/Reviewer loop, and the staffing adapter. The obvious consolidation is one builder for all
four, and it is the wrong one. They differ for real reasons: the fleet writer runs against a trusted
client root where `--permission-mode auto` reaches a reviewer, while a staffing writer is
non-interactive and would sit there asking nobody; the loop's Reviewer keeps Bash because its
findings rest on checks it runs. Forcing one command shape would flatten distinctions that earned
their place.

What actually drifted, four separate times in a single day, was never the shape. It was the small
set of facts below, each of which any launcher can get wrong on its own:

- a sandbox bypass reached the read-only fleet auditor and stood for two days;
- a staffing writer paired two Codex flags that refuse each other;
- a staffing writer used a permission mode that waits for a human who is not there;
- three of the four offered an advisory role the operator's whole external tool inventory.

So this module is a checker, not a builder. Each launcher composes its own command and then submits
it here. Drift becomes a failed assertion in a test that enumerates all four, rather than something
noticed when a fifth thing goes wrong.
"""

from __future__ import annotations

CLAUDE = "claude"
CODEX = "codex"
SUPPORTED_VENDORS = (CLAUDE, CODEX)

# Tokens that mean "this process can change the worktree". Read back off a finished command rather
# than tracked alongside it, so a launcher cannot claim one thing and build another.
WRITING_TOKENS = {
    CLAUDE: ("acceptEdits", "auto", "bypassPermissions"),
    CODEX: ("--approve-for-me", "workspace-write", "danger-full-access"),
}

# Flags no role may ever carry. `--dangerously-bypass-approvals-and-sandbox` is here because it was
# here in practice: it sat on a base command for two days, reaching a process whose entire purpose
# was to be incapable of writing.
FORBIDDEN_FLAGS = {
    CLAUDE: ("--dangerously-skip-permissions",),
    CODEX: ("--dangerously-bypass-approvals-and-sandbox",),
}

# Values no role may ever be given, whatever option carries them and however it is spelled. Kept
# apart from the flags above because the first version compared both against the raw argv, and an
# argument list holds `--permission-mode=bypassPermissions` as a single element: the forbidden token
# was inside it and `in argv` never saw it. Both of these passed a writer check that way.
FORBIDDEN_VALUES = {
    CLAUDE: ("bypassPermissions",),
    CODEX: ("danger-full-access",),
}

# The values each security-critical option is allowed to take. A value outside this set is refused
# rather than read as harmless: the checker cannot judge authority it does not recognize, and the
# failure of the first version was precisely that an unrecognized spelling read as read-only.
# Extending a vendor's vocabulary is a deliberate edit here, which is the point.
KNOWN_VALUES = {
    CLAUDE: {"--permission-mode": ("acceptEdits", "auto", "bypassPermissions", "dontAsk", "plan",
                                   "default")},
    CODEX: {"-s": ("read-only", "workspace-write", "danger-full-access"),
            "--sandbox": ("read-only", "workspace-write", "danger-full-access")},
}

# What an advisory role must carry so that it is not merely denied the operator's external tool
# servers, but never offered them. Measured 2026-09-18: the denial already held; this means the
# denial is not the only thing holding.
ADVISORY_NARROWING = {CLAUDE: "--strict-mcp-config", CODEX: "--ignore-user-config"}


class LaunchInvariantError(RuntimeError):
    """A command that violates one of these is not launched. Every case fails closed."""


# Security-critical options whose *value* decides what a command may do. Presence is not enough:
# these are read as values, so `--permission-mode=acceptEdits` and `--permission-mode acceptEdits`
# are the same fact. The first version compared tokens against the raw argv and missed the equals
# form entirely, and accepted a bare `read-only` sitting anywhere in the list as if it were a
# sandbox value.
VALUED_OPTIONS = {
    CLAUDE: ("--permission-mode", "--tools"),
    CODEX: ("-s", "--sandbox", "-m", "--model"),
}

# Built-in tools that can change the worktree. An advisory allowlist naming any of them is not an
# advisory allowlist, whatever else it says.
WRITING_TOOLS = ("Write", "Edit", "MultiEdit", "NotebookEdit", "Bash")


def option_values(vendor: str, argv: list[str]) -> dict[str, list[str]]:
    """Read security-critical options and their values, in either `--opt value` or `--opt=value`.

    Unknown options are ignored; this reads the facts that decide authority, it does not try to be a
    parser for every flag a vendor has.
    """
    names = VALUED_OPTIONS[vendor]
    found: dict[str, list[str]] = {name: [] for name in names}
    index = 0
    while index < len(argv):
        token = argv[index]
        name, _, inline = token.partition("=")
        if name in names:
            if inline:
                found[name].append(inline)
            elif index + 1 < len(argv):
                found[name].append(argv[index + 1])
                index += 1
            else:
                # A security-critical option with no value is a malformed command, not a permissive
                # one. Recorded so the caller's check refuses rather than reading past it.
                found[name].append("")
        index += 1
    return found


def writes(vendor: str, argv: list[str]) -> bool:
    """Whether this command could change the worktree, judged only from the command itself."""
    if vendor not in SUPPORTED_VENDORS:
        raise LaunchInvariantError(f"unknown vendor {vendor!r}")
    values = option_values(vendor, argv)
    if vendor == CLAUDE:
        modes = values["--permission-mode"]
        if any(mode in WRITING_TOKENS[CLAUDE] for mode in modes):
            return True
        # A read-only permission mode that hands over a writing tool is not read-only.
        allowlists = values["--tools"]
        return any(tool in allowlist.split(",") for allowlist in allowlists
                   for tool in WRITING_TOOLS)
    sandboxes = values["-s"] + values["--sandbox"]
    if any(box in ("workspace-write", "danger-full-access") for box in sandboxes):
        return True
    return "--approve-for-me" in argv


def check(vendor: str, argv: list[str], *, may_write: bool, restrict_tools: bool = True) -> None:
    """Refuse a command that contradicts the role it claims to serve.

    `restrict_tools` is False for the Executor/Reviewer's Reviewer alone, which keeps its default
    tools because a read-only allowlist would take Bash with it and with Bash the checks its
    findings rest on. It still gets the advisory narrowing; the exception is about which built-in
    tools it keeps, not about what it may reach outside the process.
    """
    if vendor not in SUPPORTED_VENDORS:
        raise LaunchInvariantError(
            f"vendor {vendor!r} has no verified launch contract; supported: {list(SUPPORTED_VENDORS)}"
        )
    # Everything that decides authority is read before anything that decides role. The first
    # version checked write capability first, so a malformed or unrecognized security-critical
    # value could satisfy the writer check and never reach the questions below.
    values = option_values(vendor, argv)
    if any("" in vals for vals in values.values()):
        raise LaunchInvariantError(
            f"{vendor} command has a security-critical option with no value: {argv}"
        )
    for option, allowed in KNOWN_VALUES[vendor].items():
        unknown = [value for value in values.get(option, []) if value not in allowed]
        if unknown:
            raise LaunchInvariantError(
                f"{vendor} option {option} carries {unknown}, which this checker does not "
                f"recognize; known values are {list(allowed)}. An unrecognized value is refused "
                "rather than assumed harmless"
            )

    flags = [flag for flag in FORBIDDEN_FLAGS[vendor]
             if any(token.partition("=")[0] == flag for token in argv)]
    if flags:
        raise LaunchInvariantError(
            f"{vendor} command carries {flags}, which no role may carry"
        )
    # Read from parsed values, so `--permission-mode bypassPermissions` and
    # `--permission-mode=bypassPermissions` are the same fact, and so a forbidden word sitting in a
    # prompt or a filename is not mistaken for one.
    given = [value for vals in values.values() for value in vals]
    banned = [value for value in FORBIDDEN_VALUES[vendor] if value in given]
    if banned:
        raise LaunchInvariantError(
            f"{vendor} command is given {banned}, which no role may be given"
        )

    if writes(vendor, argv) != may_write:
        claimed = "write" if may_write else "advisory"
        raise LaunchInvariantError(
            f"{vendor} command claims to be {claimed} but its arguments say otherwise: {argv}"
        )
    if vendor == CODEX and may_write and (values["-s"] or values["--sandbox"]) \
            and "--approve-for-me" in argv:
        raise LaunchInvariantError(
            "codex refuses --sandbox together with --approve-for-me; auto-review already implies "
            "the workspace-write sandbox"
        )
    if not may_write:
        narrowing = ADVISORY_NARROWING[vendor]
        if narrowing not in argv:
            raise LaunchInvariantError(
                f"an advisory {vendor} command must carry {narrowing} so the operator's external "
                "tool inventory is never offered to it"
            )
        if restrict_tools and vendor == CLAUDE and not values["--tools"]:
            raise LaunchInvariantError(
                "an advisory claude command must name the built-in tools it may use"
            )
        if vendor == CODEX:
            sandboxes = values["-s"] + values["--sandbox"]
            # The value, not the string. A bare `read-only` appearing anywhere in the list, in a
            # prompt or as a stray argument, used to satisfy this.
            if not sandboxes or any(box != "read-only" for box in sandboxes):
                raise LaunchInvariantError(
                    "an advisory codex command must name read-only as its sandbox value, once"
                )
