# Rule Writing

Canonical model for writing or rewriting any `AGENTS.md` rule statement, in this repository or a
downstream client. Prior art: `context-budgeting/SKILL.md`'s trigger definition and `DOC-003`
already state parts of this; this file consolidates them into one process rather than restating
them separately (`AB-RESEARCH-001`).

**Audience**: human contributors and coding agents.

## The process

For every rule, work through these steps in order. CLASSIFY and ACTIVATE apply to each clause the
rule contains; the remaining steps apply to the rule as a whole:

1. **CLASSIFY** - Is this clause a conditional rule, a persistent invariant, or a gate (a
   lifecycle checkpoint)? Classify clauses, never whole rule IDs. Most rules hold exactly one
   clause, which makes the distinction invisible and lets the error survive unnoticed; a rule
   holding several holds one form per clause. There is no fourth "composite" form: a label meaning
   "inspect the clauses separately" would give ACTIVATE nothing to act on, and reaching for one is
   the symptom of having classified an ID instead of its clauses. Splitting a well-formed
   multi-clause rule into separate IDs is not a fix for that either, and costs budget for nothing.
2. **DEDUP** - Does another rule ID already own this behavior or mechanism? If yes, reference the
   owning rule ID; do not restate its mechanic.
3. **ACTIVATE** - Phrase each clause according to its own form:
   - Conditional: state the explicit trigger first ("When \<condition\>, ...").
   - Persistent: no artificial trigger; state the standing requirement directly.
   - Gate: name the explicit lifecycle point ("Before \<checkpoint\>, ..." or
     "At \<checkpoint\>, ...").
4. **DIRECT** - State the required action or state positively. First attempt a positive
   formulation, and prefer the strongest one available: a closed set of permitted actions, which
   excludes everything outside it without having to name any of it. Use a prohibition only for a
   hard boundary (safety, authority, or integrity) when an equivalent positive formulation would
   either reintroduce the excluded case or lose precision.
   Decision test: (a) can the required state be expressed positively without loss? If yes, use
   that. If no: (b) would the positive version have to restate the excluded case anyway, or would
   it blur the boundary? If yes, a prohibition is justified. The test is not the author's
   impression that a prohibition "feels clearer."
5. **CHECK** - Name a concrete, observable check when one exists: an exact command, script, test,
   guard condition, generated artifact, or other externally inspectable result. An agent's own
   claim of having checked is never verification. If no objective check exists, omit Verify rather
   than substitute a self-check.
6. **REFERENCE** - Keep the critical minimum inline; move procedure, rationale, examples, and edge
   cases to one canonical file, referenced by exact path. This may be a passive citation
   (`[Details: <path>.]`) or a mandatory imperative ("load `<path>` first, unconditionally") when
   the detail must genuinely be read before acting, not just available on demand. Either phrasing
   is still one REFERENCE, not a second clause -- do not let a mandatory-load instruction attached
   to a Persistent or Gate rule get mistaken for a second, independently classified rule.
7. **COMPRESS** - Remove anything already encoded by the rule's own ID, priority tag, file
   location, or another authoritative rule. Do not repeat metadata or mechanics already available
   elsewhere.
8. **TEST** - For new mechanisms, material rewrites, or historically unreliable rules, compare
   candidate wording against a baseline on a representative sample containing both applicable and
   non-applicable cases; measure compliance and false activation. A brand-new rule has no
   "previous wording" to compare against, hence baseline/candidate, not before/after. Not every
   rewritten rule needs a live test; reserve it for the cases just named.

## Canonical forms

```
Conditional: <ID> [<Pn>]: When <condition>, <positive required action>.
             [Verify: <concrete check>.] [Details: <exact path>.]

Persistent:  <ID> [<Pn>]: <positive standing requirement>.
             [Verify: <concrete check>.] [Details: <exact path>.]

Gate:        <ID> [<Pn>]: Before/At <checkpoint>, <positive required action or state>.
             [Verify: <concrete check>.] [Details: <exact path>.]
```

These are clause shapes. A rule ID carrying several clauses renders one after another in the same
entry, each in its own form, with a single trailing `Verify`/`Details` for the whole rule.
`AB-WIKI-001` is the worked example: a Persistent invariant about conflict resolution, a
Conditional sync trigger, and a Gate before writing a page, correctly formed and correctly living
under one ID because they share one subject and one owner.

## Core principles

- One mechanism has one owning rule.
- Triggers appear only where applicability is conditional.
- Required behavior is stated as the desired action or state.
- A prohibition is justified only when a positive equivalent would reintroduce the excluded case
  or blur the boundary, not by an author's impression that it "reads clearer."
- Verification means observable evidence, not agent self-report.
- Critical behavior stays inline; details live canonically elsewhere.
- Every word must contribute behavior, applicability, verification, or routing.

## Worked example: DIRECT's decision test

`AB-ROADMAP-004` before: `Status is Idea, Open, In Progress, Done, Blocked, Deferred, or Archived;
never another.` Applying the test: the required state (the closed set of valid statuses) is
expressible positively without loss, because naming the allowed set already excludes everything
else. `Allowed statuses are Idea, Open, In Progress, Done, Blocked, Deferred, and Archived.` loses
nothing and drops "never another" as pure restatement.

`AB-GIT-001` was long cited here as the opposite result, on the reasoning that a positive
equivalent would lean on an unbounded, implicit "everything else" and so be less mechanically
checkable than naming the forbidden verbs. Observation refuted that. The enumeration
(`commit`, `push`, `merge`, `rebase`, `reset`, `checkout`, `switch`, `tag`) silently omitted
`restore` and `clean`, which discard uncommitted work without touching history, and an agent ran
one. A denylist is complete only up to its author's imagination, and its gaps are invisible
because the list still looks finished. The permitted set is short, genuinely closed, and *more*
mechanically checkable, not less: a candidate command is tested against a finite allowlist,
whereas a denylist can only be tested for membership it may never have been given. `AB-GIT-001`
now states that permitted set and keeps the verbs as illustrations after "including", where their
incompleteness is harmless.

The prohibition genuinely stays where the permitted set cannot be closed. `AB-SAFE-001`'s
`Never print, log, or send a secret value` has no finite allowlist behind it -- the positive
equivalent would be "print only non-secrets", which restates the excluded case and decides
nothing. That is what failing test (b) actually looks like: not that enumerating feels safer, but
that no bounded set of permitted actions exists to enumerate instead.

## Known overlap

DEDUP (step 2) and COMPRESS (step 7) both catch restated ownership, at different times: DEDUP
during drafting, COMPRESS as a final pass over the assembled sentence. This is deliberate
redundancy, not an error to resolve.

## External research: positive requirement versus prohibition framing

Two published findings bear directly on DIRECT's positive-over-prohibition preference and are not
yet reconciled with each other; recorded here so neither is lost or silently assumed resolved.

- Jang, Ye, and Seo, "Can Large Language Models Truly Understand Prompts? A Case Study with
  Negated Prompts," PMLR 203:52-62 (2023),
  <https://proceedings.mlr.press/v203/jang23a.html>. Evaluating negated prompts across model
  sizes (125M-175B) shows an *inverse* scaling law: larger models perform *worse* on negated
  prompts, the opposite of the usual scaling trend. Negation is harder for a model to parse
  correctly on first read, and that gap widens, not narrows, with model size.
- Gamage, "Omission Constraints Decay While Commission Constraints Persist in Long-Context LLM
  Agents," arXiv:2604.20911 (2026), <https://arxiv.org/abs/2604.20911>. A 4,416-trial study across
  12 models and 8 providers at six conversation depths found requirement-type constraint
  compliance ("always do X") falls from 73% at turn 5 to 33% at turn 16, while prohibition-type
  constraint compliance ("never do X") holds at 100% regardless of depth. Positive requirements
  are silently dropped by omission as context grows; prohibitions keep suppressing the forbidden
  action.

**The tension**: Jang argues prohibitions are harder to parse correctly in a single read, especially
for larger models -- favoring positive framing. Gamage argues prohibitions are far more durable
across a long session -- favoring prohibition framing for anything that must hold many turns later.
Neither paper contradicts the other; they measure different failure modes (first-read comprehension
versus multi-turn compliance decay). DIRECT's existing decision test is evidence-backed for
immediate clarity, but is not yet evidence-backed for long-session survival, which matters most for
exactly the highest-stakes Persistent invariants (safety, authority, integrity boundaries) that
`090`'s prohibition-to-directive conversion is rewriting. This is open work under roadmap item 092,
not a settled conclusion in either direction.

## Provenance and verification status

Drafted through iterative external review (2026-09-06), refined against this repository's own
`AB-UPDATE-002` rewrite as the first real test case. The three-way CLASSIFY split (conditional /
persistent / gate) has not yet been run across this repository's full existing rule set to confirm
every rule sorts cleanly; that check, and a live compliance/false-activation measurement per TEST,
are open work under roadmap item 092.
