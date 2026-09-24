---
name: ab-quality-check
description: Review this repository against the Agent Base quality conditions, including the ones no script can decide, and report findings with their IDs. Use on "AB check", "check AB", "AB quality", or an equivalent request in any language. Never syncs, updates, or changes the agent-base submodule.
---

# AB Quality Check

Read `.agents/agent-base/standards/ab-quality.md` first; it owns the conditions, their IDs, and
their severities. This procedure works through them and reports. It changes nothing on its own.

This skill judges the Agent Base/downstream relationship. It does not audit whether Purpose,
Vision, Strategy, and Goals remain internally coherent; `venture-review` owns that periodic check,
while `venture-discovery` owns any human-approved repair to Venture intent.

## This is not an update

`AB check` and `Update AB` are neighbouring phrases with opposite effects, and only one of them
mutates the repository. This skill never fetches, never syncs, never moves the `_sub/agent-base`
pointer, and never runs `ab.sh`. If the request is genuinely about updating, `AB-SYNC-001` owns it
and this skill is the wrong one. When the wording could plausibly mean either, ask which was meant
rather than guessing: `AB-INPUT-001` requires that, because the two answers differ in whether
anything is written at all.

## Run the mechanical layer first

```bash
bash "$(git rev-parse --show-toplevel)/agent-base-guard.sh"
```

The guard already decides every mechanical condition and prints the failures with their IDs. Do not
re-derive them by hand and do not contradict them. Carry its findings into the report unchanged.

## Then judge what a script cannot

Work through the judged conditions in the standard. For each, read the actual artifact rather than
its summary, and reach a verdict with evidence:

- **The Venture teaser.** Does it state what this Venture optimizes for and what it refuses, or
  only what it hopes to become? Would an agent that read nothing else know when this Venture is at
  stake? Would it open the canonical sources, or feel already informed? A teaser that feels
  sufficient is the failure mode, not the goal.
- **Open requests.** Does each one carry evidence somebody else could reproduce, and is it as
  generic as it claims? A request that only makes sense here is a downstream task wearing the wrong
  label, and saying so early is kinder than a refusal months later.
- **Roadmap honesty.** Does an `In Progress` item still describe work that is actually underway?

Judge the artifact, never the person who wrote it, and prefer a concrete rewrite over a complaint.
For anything you would change, show the replacement text.

## Report

Before the table, check the hard stop directly:

```bash
python3 scripts/venture.py --root . context
```

A non-zero exit means Identity, Purpose, Vision, Strategy, or Goals is missing or unfilled. Report
`Venture compatible: No - Venture Context unavailable; load venture-discovery first` and stop
there; the ten conditions below are not individually meaningful until this clears.

Give one table, most severe first:

| ID | Condition | Verdict | Evidence | Suggested fix |
| --- | --- | --- | --- | --- |

State plainly when everything passes; a quality check that always finds something is a check
nobody trusts. End with one line: `Venture compatible: Yes` when all ten conditions in the table
are clear, otherwise `Venture compatible: No (N of 10 conditions below standard)`. This is computed
fresh from the table above, never cached or written anywhere; see
`.agents/agent-base/standards/ab-quality.md`'s Compatibility section for what the term means and
why the Categories tree (Products, Feedback, Resources, ...) is deliberately not part of it. Then
stop. Every fix is the human's to approve, including the ones you drafted, and
a finding may be deferred without argument: say what it costs to leave it and move on.

If a condition seems wrong, or a repeated finding is really a gap in agent-base rather than in this
repository, do not work around it. Record a request under `.agents/requests/` per `AB-REQUEST-001`.
