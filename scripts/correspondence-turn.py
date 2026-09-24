#!/usr/bin/env python3
"""Whose turn it is in an Agent Correspondence, read off the directory (roadmap item 156).

The predicate this script answers, stated before any code reads it:

    Given a directory of timestamped messages named `<sender>_to_<recipients>`, and the DEPARTMENT I hold,
    is there a message addressed to me that I have not answered since, and which one is it?

It exists so that a human relaying between agents does not have to remember. Agents writing to each
other through a directory cannot poll it: each sees the tree only while its own turn runs, so a
message sits unread until something wakes the other one. The human is the transport, and the cost of
that transport is whatever it takes to say "your turn". One keystroke is the target, and a keystroke
only works if the agent can work out the rest itself.

Correspondents are addressed by **DEPARTMENT**, not by MODEL or SESSION nickname. `opus` names a MODEL
that may be replaced, and `astra` names a SESSION that exists for an afternoon; neither survives the
thing it was addressing. A DEPARTMENT does, and agent-base already has a vocabulary of them in the
Venture DEPARTMENTS, so a message runs between Engineering and Architecture the way a memo runs
between departments of a firm. The sending WORKER is recorded inside the message, where
accountability belongs, rather than in the address, where it would rot.

The turn is derived, never assumed. Two consecutive wake-ups with no new message in between must
produce "nothing for you" rather than a second answer to a message already answered, because an
agent that invents a turn is worse than a human who forgets to give it one.
"""

from __future__ import annotations

import argparse
from pathlib import Path
import re
import sys
from typing import NamedTuple

# `YYMMDD_HHMM_<sender>_to_<recipients>_<topic>.md`.
#
# Sender and recipients are bare DEPARTMENT names so that a human reading `ls` can see the conversation
# without opening anything, which is most of why the convention is worth keeping. Several recipients
# are joined with `+`: `architecture_to_engineering+quality_...`. That spelling was chosen over a
# recipient list inside the file because addressing that only a parser can see is addressing the
# human relaying the messages cannot check.
MESSAGE = re.compile(
    r"^(?P<stamp>\d{6}_\d{4})_(?P<sender>[a-z0-9-]+)_to_(?P<recipients>[a-z0-9+-]+)_(?P<topic>.+)\.md$"
)

# Files that live in the correspondence and are not messages. `CURRENT_STATE.md` is the one
# coordination file updated in place and `ROSTER.md` says which WORKER serves each DEPARTMENT;
# everything else there is an immutable message.
NOT_MESSAGES = {"CURRENT_STATE.md", "ROSTER.md", "README.md"}

EVERYONE = "all"


class Message(NamedTuple):
    path: Path
    stamp: str
    sender: str
    recipients: tuple[str, ...]
    topic: str

    def addressed_to(self, department: str) -> bool:
        return department in self.recipients or EVERYONE in self.recipients

    @property
    def addressees(self) -> str:
        return " and ".join(self.recipients)


class Turn(NamedTuple):
    state: str          # "yours", "theirs", "empty", "unknown-department"
    message: Message | None
    reason: str


def read_messages(directory: Path) -> tuple[list[Message], list[str]]:
    """Every well-formed message, oldest first, plus the names of files that are not messages.

    Unrecognized filenames are reported rather than ignored. A message nobody can address is a
    message that will be missed, and silently skipping it is how it gets missed twice.
    """
    messages: list[Message] = []
    unrecognized: list[str] = []
    try:
        entries = sorted(directory.iterdir())
    except OSError as error:
        raise SystemExit(f"correspondence-turn: cannot read {directory}: {error}")
    for entry in entries:
        if not entry.is_file() or entry.name in NOT_MESSAGES:
            continue
        match = MESSAGE.match(entry.name)
        if not match:
            unrecognized.append(entry.name)
            continue
        messages.append(Message(
            path=entry,
            stamp=match.group("stamp"),
            sender=match.group("sender"),
            recipients=tuple(match.group("recipients").split("+")),
            topic=match.group("topic"),
        ))
    # Sorted by the stamp in the name, then by name, so two messages in the same minute stay stable.
    messages.sort(key=lambda m: (m.stamp, m.path.name))
    return messages, unrecognized


def whose_turn(messages: list[Message], department: str) -> Turn:
    """The newest message addressed to me, unless I have already answered its sender.

    Stated that way rather than as "the newest message decides", which is the same rule while there
    are two correspondents and the wrong one as soon as there are three: with Engineering writing to
    Architecture and then to Quality, Architecture would lose its turn to a message that was never
    addressed to it.

    "Unless I have already answered its sender" is the whole of the repeated-signal guard. Answering,
    then being woken again with nothing new, finds my own later reply and reports nothing rather than
    answering twice. It is scoped to the sender rather than to my messages in general, because with
    three correspondents a message I sent to somebody else is not an answer to this one. No state is
    kept anywhere; the directory already records it.
    """
    if not messages:
        return Turn("empty", None, "the correspondence holds no messages yet")

    to_me = [m for m in messages if m.addressed_to(department) and m.sender != department]
    if not to_me:
        # Checked only once nothing is addressed to me, because a message to `all` addresses a department
        # that appears nowhere else in the directory and is still that department's turn.
        known = {m.sender for m in messages} | {r for m in messages for r in m.recipients}
        if department not in known:
            return Turn("unknown-department", messages[-1],
                        f"no message here is from or to {department!r}; the DEPARTMENTS in use are "
                        f"{', '.join(sorted(known - {EVERYONE}))}")
        newest = messages[-1]
        return Turn("theirs", newest,
                    f"nothing here is addressed to you; the newest is from {newest.sender} to "
                    f"{newest.addressees}")

    waiting = to_me[-1]
    replies = [m for m in messages if m.sender == department and m.addressed_to(waiting.sender)]
    if replies and replies[-1].stamp >= waiting.stamp:
        return Turn("theirs", replies[-1],
                    f"you answered {waiting.sender} already, and nobody has written since")
    return Turn("yours", waiting, f"{waiting.sender} is waiting on you")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--as", dest="department", required=True,
                        help="the DEPARTMENT you hold in this correspondence, e.g. engineering")
    parser.add_argument("--dir", type=Path, default=Path(".agents/correspondence"),
                        help="the correspondence directory (default: .agents/correspondence)")
    args = parser.parse_args(argv)

    if not args.dir.is_dir():
        print(f"No correspondence directory at {args.dir}; there is nothing to react to.")
        return 1

    messages, unrecognized = read_messages(args.dir)
    turn = whose_turn(messages, args.department)

    for name in unrecognized:
        print(f"note: {name} is not named like a message and was not considered", file=sys.stderr)

    if turn.state == "yours":
        print(f"Your turn: {turn.message.path}")
        print(f"  from {turn.message.sender}, {turn.message.stamp}, on {turn.message.topic}")
        return 0

    if turn.state == "unknown-department":
        # Distinguished from an ordinary quiet turn on purpose. A mistyped department otherwise reports
        # "nothing for you", which is indistinguishable from the truth and reads as permission to
        # stop.
        print(f"Unknown DEPARTMENT: {turn.reason}.")
        return 2

    print(f"Nothing for you: {turn.reason}.")
    if turn.message is not None:
        print(f"  newest: {turn.message.path}")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
