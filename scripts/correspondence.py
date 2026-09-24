#!/usr/bin/env python3
"""Open an Agent Correspondence, or join one, without the human doing the clerical work (item 162).

The practice works and had been used once, by the two agents who invented it, because starting one
meant the human created a directory, wrote a roster, decided which DEPARTMENTS each side held, and
told both SESSIONS which they were. That is setup handed to the person the practice exists to spare.

    python3 scripts/correspondence.py open --as engineering --with architecture \\
        --topic "the lease protocol" --because "boundaries and contracts are Architecture's"
    python3 scripts/correspondence.py join --as architecture
    python3 scripts/correspondence.py departments

`open` creates the directory and the roster, and prints the **invitation**: the text to paste into a
second window, complete, needing no editing. `join` is what that second SESSION runs; it finds the
roster, records its WORKER, and says what it is joining.

The human still opens the second window. That is the boundary rather than a gap: the relay is the
human, and an agent that could start another agent is a different feature with different risks.
Everything either side of that one manual act is prepared for them.

Correspondence addresses are Venture DEPARTMENTS, validated against the catalogue rather than
invented, because the DEPARTMENT that owns a subject is already the one that should answer about it.
"""

from __future__ import annotations

import argparse
import datetime as dt
import os
from pathlib import Path
import re
import subprocess
import sys

DIRECTORY = Path(".agents/correspondence")
ROSTER = "ROSTER.md"
# Two places, because agent-base itself is the repository that produces the distributed copy and
# does not receive one. Looking only where a client keeps it meant this could not be used in the
# repository that invented the practice, which is the shape of dogfooding failure `AB-DOGFOOD-001`
# exists to prevent.
CATALOGUES = (Path(".agents/agent-base/departments/_DEPARTMENTS_CATALOG.md"),
              Path("template/.agents/agent-base/departments/_DEPARTMENTS_CATALOG.md"))
ROW = re.compile(r"^\|\s*\[([A-Za-z][A-Za-z ]*)\]\(([a-z-]+)\.md\)\s*\|")


class CorrespondenceError(RuntimeError):
    """Every refusal here, phrased for a session that has not read the standard yet."""


def repository_root(start: Path | None = None) -> Path:
    found = subprocess.run(["git", "rev-parse", "--show-toplevel"], cwd=str(start or Path.cwd()),
                           capture_output=True, text=True, check=False)
    if found.returncode:
        raise CorrespondenceError("not inside a Git worktree; a correspondence belongs to a repository")
    return Path(found.stdout.strip())


def departments(root: Path) -> list[str]:
    """The DEPARTMENTS a correspondent may address, read from the distributed catalogue.

    Read rather than listed here so that a client which has gained or renamed a Department does not
    need this file changed. A repository with no catalogue has no Venture vocabulary to draw on, and
    saying so is better than inventing one.
    """
    body = None
    for candidate in CATALOGUES:
        try:
            body = (root / candidate).read_text(encoding="utf-8")
            break
        except OSError:
            continue
    if body is None:
        raise CorrespondenceError(
            f"no Departments catalogue under {root}; looked in "
            f"{[str(c) for c in CATALOGUES]}. Addresses are Venture DEPARTMENTS, and without the "
            "catalogue there is nothing to choose from."
        )
    found = [match.group(2) for line in body.splitlines() if (match := ROW.match(line))]
    if not found:
        raise CorrespondenceError(f"the Departments catalogue under {root} lists none")
    return found


def worker() -> str:
    """The WORKER this SESSION identifies, or an honest unknown.

    A WORKER is organizational identity, not a MODEL, SESSION, or RUNTIME. A caller may supply it
    through `AB_WORKER_ID`; otherwise the roster says `unstated` rather than inventing a person.
    """
    return os.environ.get("AB_WORKER_ID", "unstated")


def roster_path(root: Path) -> Path:
    return root / DIRECTORY / ROSTER


def read_roster(root: Path) -> dict[str, str]:
    """DEPARTMENT to WORKER, from the roster's table. Absent file means no correspondence yet."""
    try:
        body = roster_path(root).read_text(encoding="utf-8")
    except OSError:
        return {}
    held = {}
    for line in body.splitlines():
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if (len(cells) >= 2 and cells[0] and cells[0] not in ("Department", "Role", "---")
                and "-" * 3 not in cells[0]):
            held[cells[0]] = cells[1]
    return held


def write_roster(root: Path, held: dict[str, str], topic: str, because: str) -> Path:
    path = roster_path(root)
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        "# Roster",
        "",
        "The current WORKER for each addressed DEPARTMENT. A message filename addresses a",
        "DEPARTMENT; this file records its WORKER separately, so a change of WORKER is a line here",
        "rather than a break in the correspondence.",
        "",
        f"**Topic**: {topic}",
        f"**Departments chosen because**: {because}",
        f"**Opened**: {dt.date.today().isoformat()}",
        "",
        "| Department | Worker |",
        "| --- | --- |",
    ]
    lines += [f"| {department} | {holder} |" for department, holder in sorted(held.items())]
    lines += [
        "",
        "A DEPARTMENT with no recorded WORKER is free: a SESSION joining this correspondence may",
        "record itself by running `correspondence.py join --as <Department>`.",
        "",
    ]
    path.write_text("\n".join(lines), encoding="utf-8")
    return path


def here(root: Path, target: Path) -> str:
    """How the joining session should refer to a file: relative when it is theirs, absolute when not.

    A client that has not received these scripts is the ordinary case, not an edge: this repository
    distributes them and a downstream that is behind a release does not have them yet. An invitation
    naming `scripts/correspondence.py` in a repository that has no such file is an invitation that
    cannot be followed, which is the one thing it exists not to be.
    """
    try:
        return str(target.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(target.resolve())


def invitation(root: Path, mine: str, theirs: list[str], topic: str, because: str) -> str:
    """The text the human pastes into the second window. Complete, and needing no editing."""
    others = ", ".join(theirs)
    command = f"python3 {here(root, Path(__file__))}"
    standard = root / ".agents/agent-base/standards/agent-correspondence.md"
    reading = (f"Then read `{here(root, standard)}` for how a message is named and what it must"
               if standard.exists() else
               "There is no copy of the Agent Correspondence standard in this repository, so what"
               " a message must")
    return "\n".join([
        f"You are joining an Agent Correspondence in {root}.",
        "",
        f"Your DEPARTMENT is **{others}**. The other side is **{mine}**.",
        f"The topic is: {topic}",
        f"The DEPARTMENTS were chosen because {because}",
        "",
        "Start by running this, which records your WORKER and tells you what is waiting:",
        "",
        f"    {command} join --as {theirs[0]}",
        "",
        f"{reading} contain is in the message already waiting for you: a heading that states the",
        "finding, From, To, Phase, Status, the repository state you inspected, the substance, and",
        "last what you ran and what you did not. Messages are immutable: a correction is the next",
        "message, never an edit to a sent one.",
        "",
        "When the human types a bare `r`, run",
        f"`{command} turn --as {theirs[0]}` and react to whatever it names, or say plainly that",
        "there is nothing for you. Neither of you has to remember whose turn it is; the directory",
        "records it.",
    ])


def _open(args) -> int:
    root = repository_root()
    known = departments(root)
    for department in [args.department] + args.with_departments:
        if department not in known:
            raise CorrespondenceError(
                f"{department!r} is not a DEPARTMENT in this repository; the DEPARTMENTS are {known}"
            )
    if args.department in args.with_departments:
        raise CorrespondenceError("a correspondence needs two different DEPARTMENTS, not one twice")

    existing = read_roster(root)
    if existing:
        raise CorrespondenceError(
            f"a correspondence is already open here for {sorted(existing)}. Join it with "
            "`join --as <Department>`, or say so rather than starting a second one."
        )

    held = {args.department: worker()}
    held.update({department: "" for department in args.with_departments})
    path = write_roster(root, held, args.topic, args.because)

    print(f"Opened {path.parent}")
    print(f"  your DEPARTMENT is {args.department}, WORKER {worker()}")
    print(f"  waiting for {', '.join(args.with_departments)}")
    print()
    print("=" * 72)
    print("Paste everything between these lines into the other window:")
    print("=" * 72)
    print(invitation(root, args.department, args.with_departments, args.topic, args.because))
    print("=" * 72)
    return 0


def _join(args) -> int:
    root = repository_root()
    held = read_roster(root)
    if not held:
        raise CorrespondenceError(
            f"no correspondence at {root / DIRECTORY}; open one with `open --as <Department> "
            "--with <Department> --topic ... --because ...`"
        )
    if args.department is None:
        free = [department for department, holder in held.items() if not holder]
        print(f"Correspondence at {root / DIRECTORY}")
        for department, holder in sorted(held.items()):
            print(f"  {department:<14} {holder or 'free'}")
        if free:
            print(f"\nJoin with `join --as {free[0]}`.")
        else:
            print("\nEvery DEPARTMENT has a recorded WORKER. Replacing one is a decision for the "
                  "human rather than for you.")
        return 0

    if args.department not in held:
        raise CorrespondenceError(
            f"{args.department!r} is not a DEPARTMENT in this correspondence; the DEPARTMENTS are {sorted(held)}")
    if held[args.department] and not args.take_over:
        raise CorrespondenceError(
            f"{args.department} records WORKER {held[args.department]}. Replacing it is the human's decision; "
            "pass --take-over only when they have made it."
        )

    held[args.department] = worker()
    body = roster_path(root).read_text(encoding="utf-8")
    topic = next((line.split("**:", 1)[1].strip() for line in body.splitlines()
                  if line.startswith("**Topic**")), "not recorded")
    because = next((line.split("**:", 1)[1].strip() for line in body.splitlines()
                    if line.startswith("**Departments chosen because**")), "not recorded")
    write_roster(root, held, topic, because)

    print(f"Joined DEPARTMENT {args.department}, listed as WORKER {worker()}")
    print(f"  topic      {topic}")
    print(f"  other side {', '.join(d for d in sorted(held) if d != args.department)}")
    print()
    turn = subprocess.run(
        [sys.executable, str(Path(__file__).resolve().with_name("correspondence-turn.py")),
         "--as", args.department, "--dir", str(root / DIRECTORY)],
        capture_output=True, text=True, check=False)
    print((turn.stdout or turn.stderr).rstrip())
    return 0


def _roles(args) -> int:
    root = repository_root()
    for department in departments(root):
        print(department)
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    commands = parser.add_subparsers(dest="command", required=True)

    opening = commands.add_parser("open")
    opening.add_argument("--as", dest="department", required=True, help="the DEPARTMENT you hold")
    opening.add_argument("--with", dest="with_departments", nargs="+", required=True,
                         help="the DEPARTMENTS the other SESSIONS hold")
    opening.add_argument("--topic", required=True)
    opening.add_argument("--because", required=True,
                         help="one line on why these DEPARTMENTS, so the human can overrule it")

    joining = commands.add_parser("join")
    joining.add_argument("--as", dest="department", default=None,
                         help="omit to see which DEPARTMENTS have no recorded WORKER")
    joining.add_argument("--take-over", action="store_true",
                         help="only when the human has decided to displace the current holder")

    commands.add_parser("departments", aliases=["roles"])

    turning = commands.add_parser("turn")
    turning.add_argument("--as", dest="department", required=True)

    args = parser.parse_args(argv)
    try:
        if args.command == "turn":
            root = repository_root()
            return subprocess.run(
                [sys.executable,
                 str(Path(__file__).resolve().with_name("correspondence-turn.py")),
                 "--as", args.department, "--dir", str(root / DIRECTORY)], check=False).returncode
        return {"open": _open, "join": _join, "departments": _roles, "roles": _roles}[args.command](args)
    except CorrespondenceError as error:
        print(f"correspondence: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
