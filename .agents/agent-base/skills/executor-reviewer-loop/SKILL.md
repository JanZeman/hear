---
name: executor-reviewer-loop
description: Run a bounded, cross-vendor Executor/Reviewer review loop on one task -- an Executor completes the work, a Reviewer (a different vendor by default) critiques it, the Executor addresses findings, and the two repeat until they converge or a round cap is hit. Known elsewhere as the Actor-Critic, Generator-Critic, or Writer-Critic pattern, or an adversarial/multi-agent review loop. Use whenever the human asks, in any of these shapes, for a second opinion, a second agent's review, a multi-agent or two-agent review, an opposing/adversarial view, a Codex/Claude cross-check, an actor-critic or generator-critic loop, or names this skill directly; never start it implicitly on a bare "this could use review."
---

# Executor/Reviewer loop (a.k.a. Actor-Critic / Generator-Critic)

One agent (Executor) does a task; a second agent (Reviewer), by default a different vendor,
critiques the result; the Executor addresses the findings; repeat for a small bounded number of
rounds until both converge or the round cap is hit. This consistently lifts output quality above
what either agent produces alone (roadmap item 122).

**Start this only on an explicit human request** naming the task (and optionally Executor,
Reviewer, or a round budget). A vendor already being installed, a prior loop, or general "this
could use review" language is never enough on its own -- invoking a second vendor's CLI runs a
paid, external agent session (`AB-DESIGN-001`).

## What it is not

- Not a live conversation. Every round, both roles, every time, is a fresh non-interactive vendor
  CLI process -- never the current interactive session doing the round itself. This is what keeps
  every round bounded, reviewable, and identical in shape regardless of which vendor plays which
  role.
- Not a Git authority grant. The loop never gains any permission beyond what `AB-GIT-001`/
  `AB-GIT-002` already give an ordinary session in this repository. Convergence produces a result
  for the human to review and commit; nothing here stages, commits, or pushes on its own.
- Not durable state. The whole exchange lives in one scratch file outside the repository's tracked
  tree for the run's duration only, and is deleted once the result has been reported.

## Running it

The mechanics are one script, `scripts/executor-reviewer-loop.py` (stdlib only, no dependencies):

```bash
python3 scripts/executor-reviewer-loop.py --task-file <path to a plain-text task description>
```

All of the following are optional, each with a stated default:

- `--root <path>`: the project root the loop operates in (default: current directory).
- `--executor {claude,codex}`: defaults to the vendor the loop is invoked from
  (`$AGENT_BASE_VENDOR`, the same value the guard already tags sessions with).
- `--reviewer {claude,codex}`: defaults to "the other one" of the two vendors above -- a
  preference for genuinely independent review, not a same-vendor prohibition. Naming the same
  vendor for both roles explicitly is fully legitimate (e.g. the other vendor's quota is
  exhausted, or the human just wants two passes from the same model).
- `--rounds N`: round cap, default `5`, matching the diminishing-returns point observed in
  practice.

Only Claude and Codex have verified non-interactive launch contracts today (the same ones skill
002's `client-rooted-execution.md` documents for fleet updates); the "other one" default becomes
ambiguous once a third vendor gains one, at which point both roles should be named explicitly.

Write the task description to a plain text/Markdown file first (the skill invoker's job, not the
script's), then run the command above. The script prints a short final report -- how the loop
ended, how many rounds ran, and the scratch log's path -- and deletes that file once printed.
Read the log before it is gone if the outcome needs a closer look (pass `--keep-log` to keep it
for debugging).

## How a round works

Each round appends to one growing scratch Markdown file, never rewriting earlier content: the
original task once at the top, then `## Round N - Executor` and `## Round N - Reviewer Findings`
sections in order. Whichever vendor is invoked for a round receives the *entire* file so far as
its prompt and only appends its own new section.

The Reviewer's findings section has exactly one required shape at the heading level:

```
## Reviewer Findings

No findings.
```

or one `### Finding N` subsection per issue, where **no field is required, not even on a
structured finding** -- pure prose describing a problem is a fully valid finding:

```
## Reviewer Findings

### Finding 1
- **File**: relative/path.py
- **Line**: 42
- **Category**: correctness
- **Severity**: high
- **Summary**: one-sentence claim
- **Failure scenario**: concrete inputs/state -> wrong output or crash

### Finding 2
This whole caching approach looks fragile -- invalidation never fires on a runtime config reload.
```

The loop parses whichever bold-labeled fields (`File`, `Line`, `Category`, `Severity`, `Summary`,
`Failure scenario`) it recognizes in each block and treats the rest as free text.

## How the loop ends

Reported as one of:

- `converged`: the Reviewer's findings section held nothing but the convergence sentence, "No
  findings." -- both sides are satisfied. That sentence has to stand alone: a section carrying any
  other prose is read as one free-text finding, because a Reviewer raising an objection in a plain
  sentence must never be reported as agreement. The cost of the opposite error is one extra round
  the Executor answers with "nothing to fix".
- `cap_exhausted`: the round budget ran out while still trending toward convergence. Not the same
  outcome as converging; report both sides' final positions to the human rather than picking one.
- `oscillating`: the same finding (fingerprinted by category/file/summary, or by its normalized
  free text when it carries no structured field) reappeared two or more rounds after the Executor
  addressed it. Ends the loop immediately, before the cap, as genuine unresolved disagreement.
- `malformed_reviewer_output`: the Reviewer's output had no parseable `## Reviewer Findings`
  section at all, or the section was empty or held nothing but punctuation. Treated as an immediate
  stop, not as zero findings: a review that died mid-write must not be indistinguishable from one
  that approved the work.
- `vendor_invocation_failed`: a vendor CLI exited non-zero. Its partial output is kept in the log
  but never parsed as a result, so text that happens to contain the convergence sentence cannot be
  accepted from a crashed run.

## Not in v1 scope

Unbounded looping, autonomous commit/push at convergence, role-swapping mid-loop, cross-machine or
cross-repository orchestration, and more than two candidate vendors for the default "other one"
Reviewer resolution.
