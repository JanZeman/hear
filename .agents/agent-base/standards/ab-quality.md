# Agent Base Quality

Canonical registry of the conditions that keep the AB and DS relationship at a usable standard:
`AB` is agent-base, `DS` is this repository as a downstream client of it. A migration that applied
cleanly is not the same as a repository that is actually in good shape, and this is where the
difference is written down.

## Two layers, because one of them cannot be a script

| Layer | Runs | Decides |
| --- | --- | --- |
| `agent-base-guard.sh` | every session start, and whenever the guard is rerun | mechanical conditions only |
| `ab-quality-check` skill | on request, per `AB-QUALITY-001` | everything that needs judgment |

A shell script can prove that a Venture teaser exists, fits its budget, and ends on a sentence. It
cannot decide whether the teaser is any good. Anything requiring that judgment belongs to the
skill, is read by an agent, and is confirmed by the human. Do not add a judged condition to the
guard: it will either pass everything or invent false alarms, and both teach the reader to ignore
the output.

## Severity

| Severity | Session effect | Use for |
| --- | --- | --- |
| `advice` | one plain line | a nicety; nothing is wrong |
| `strong` | a prominent block naming the check ID and the exact fix, stating that it may be deferred | every quality condition |
| `blocking` | the guard exits non-zero | structural breakage where continuing is unsafe |

`strong` never changes the guard's exit code. This matters more than it looks: the guard prints
`AGENTS.md` and the Venture Context *after* its result, so a `blocking` finding silently strips the
agent's rules and decision context for that session. That is the right trade for a broken
repository and the wrong one for an unwritten teaser, which is why a quality condition is never
`blocking`.

Every `strong` finding must say, in the same breath, what to do and that it can wait. A human who
is mid-task may defer any of them; the point is that they were told, not that they were stopped.

## Conditions

Mechanical conditions are enforced by the guard. Judged conditions are worked through by the skill.

| ID | Layer | Severity | Condition |
| --- | --- | --- | --- |
| `AB-Q001` | guard | `strong` | `.agents/VENTURE.md` has an `## Identity` section that is filled in |
| `AB-Q002` | guard | `strong` | `## Identity` preferably stays within the length `venture.py` recommends; excess produces authoring guidance but never truncates session context |
| `AB-Q003` | guard | `strong` | `## Identity` is not a verbatim restatement of `## Purpose` |
| `AB-Q004` | guard | `strong` | Every answered request carries a `Blocks` value, so the way back exists |
| `AB-Q005` | guard | `strong` | Every request file has a row in `_REQUESTS_CATALOG.md`, so the index and the directory agree |
| `AB-Q006` | guard | `strong` | This repository has a wiki mapping in the operator-local `wikis.json` registry |
| `AB-Q101` | skill | `strong` | `## Identity` says what the Venture is and how it is run, in words nobody could infer from the repository |
| `AB-Q102` | skill | `strong` | A competent agent reading only the rendered `## Venture` block would open the canonical sources |
| `AB-Q103` | skill | `strong` | Open requests are genuinely generic where they claim to be, and carry reproducible evidence |
| `AB-Q104` | skill | `advice` | `In Progress` roadmap items still describe work that is actually underway |

## Compatibility

"Venture compatible" is a defined, computed term, not a feeling: `AB-VENTURE-001`'s hard stop
clears (`python3 scripts/venture.py --root . context` exits `0`) and all ten `strong`/`advice`
conditions above are clear. Nothing else counts and nothing less does. The Categories tree
(Products, Feedback, Resources, ...) is deliberately excluded: most of it is legitimately inactive
for most Ventures, and requiring it would contradict Anti-bureaucracy.

If the hard stop does not clear, compatibility is `No` outright and the ten conditions are not
individually meaningful; report that a human needs `venture-discovery` first rather than a partial
count. The verdict is computed fresh every time it is asked, by `ab-quality-check`, never stored:
a stored flag would go stale the moment one condition changes, which is exactly the failure mode
storing it would invite.

## Adding a condition

A new condition enters at `strong` and is never born `blocking`. Promotion to `blocking` is a human
decision, taken only after the condition has been clean across every known client, and recorded in
the row above. Retroactivity is automatic and intended: the guard inspects the repository as it is,
so an existing repository is measured by a condition added later, which is the whole point.

Give every condition a stable `AB-QNNN` identifier, mechanical ones in the `001` range and judged
ones in the `101` range, and cite it in whatever reports it. A finding that cannot be traced to a
row here is not a quality condition, it is an opinion.
