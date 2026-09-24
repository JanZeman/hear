---
name: venture-discovery
description: Elicit and structure a Venture's Identity, Purpose, Vision, Strategy, and Goals from the human directing it. Use when Venture Context is missing or unfilled, or when that human asks to revisit why the Venture exists; never infer human motives or objectives from repository evidence.
---

# Venture Discovery

Create decision context that reflects the human's intent, not a plausible story assembled from the
repository. The repository can reveal what exists and support follow-up questions; it cannot supply
why the human began, what makes the work worthwhile, or what they want it to become.

## Boundary

- Treat the current user as the decision authority unless the Venture names another human.
- Do not propose or write Identity, Purpose, Vision, Strategy, or Goals before hearing from that human.
- Do not reopen already approved context automatically. Use this skill again when the human asks or
  when a material contradiction makes the existing context uncertain.
- `venture-review` detects those contradictions on a cadence and hands proposed repairs here;
  `ab-quality-check` judges the Agent Base/downstream relationship, not internal Venture intent.

## Conversation

Start with one question: **Why does this Venture exist, and why did you start it?** Then ask one
high-value follow-up at a time, only until these decision boundaries are clear:

- who benefits and what value matters;
- what success or continued motivation would look like;
- constraints, especially time, resources, risk appetite, and what the Venture must not become;
- the Venture's role beside the human's other activities.

Use repository evidence to make follow-ups concrete, never to answer for the human. Avoid a fixed
questionnaire, speculative completeness, and ceremony.

## Structure and approval

Translate the answers into concise Purpose, Vision, and Strategy drafts. Propose two to five Goals
only when the human's desired outcomes are clear. Ask how each would be recognized and what outside
evidence could falsify it; do not invent ambition, thresholds, or deadlines. If useful guidance is
not practically measurable, keep it in Purpose, Strategy, a Product principle, or acceptance
criteria instead of forcing it into a Goal.

## Identity

`.agents/VENTURE.md` opens with `## Identity`: at most 25 words saying what this Venture *is*.
Draft it once Purpose and Strategy are settled, then have the human confirm or correct it. It is a
claim about the nature of the undertaking, so it is agreed rather than inferred, but waiting for
the human to compose it from scratch is not the point.

Identity sits above Purpose because every other section assumes it. Name the kind of undertaking
and how it is run, not what it does for its users, which is Purpose. The words worth spending are
the framing nobody could infer from the repository: the technology is usually visible in the code,
while "built on one owner's bounded time and meant to outgrow it" is not. Do not restate Strategy
or Goals; both are rendered into `AGENTS.md` from their own sources alongside it.

Present the complete draft and material interpretation choices. Persist it only after the human
approves. Then run `python3 scripts/venture.py --root . context`, refresh the bounded summary, and
report any remaining undefined source.
