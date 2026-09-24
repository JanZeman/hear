# Documentation

Canonical reference for how this project keeps its documentation from duplicating or
outgrowing itself, and for how design decisions get proposed before they get built.

**Audience**: human contributors and coding agents (Claude, Copilot, Warp, Codex, others).

## Keeping AGENTS.md lean (DOC-003)

`AGENTS.md` gets forced into every agent session's context in full (see the Session Start
hook). Its size is not a style preference -- it is what every session pays, every time, for
as long as the project exists. Published guidance is that oversized instruction files make
models ignore instructions wholesale rather than filter them selectively, which is a second,
independent reason beyond raw token cost.

Do not judge a rendered downstream `AGENTS.md` against a standalone 2,000-word limit. The release
gate limits static `template/AGENTS.md` to 2,500 words, while the runtime gauge measures the
complete 5,000-word session and its 2,500-word agent-base and project shares. Filling the
template's `<!-- TODO -->` placeholders spends from the composed project share like any other
addition. When context grows too large, shrink it by **moving content out, never by deleting it**
-- nothing gets shorter by losing context that mattered.

For each section, ask: does it state a rule (a short, stable, rule-ID'd sentence), or does it
explain/justify/exemplify one (rationale, examples, historical narrative, incident record)?

- Keep the former inline in `AGENTS.md`.
- Move the latter to its natural home: a standards doc here for a cross-cutting topic, a
  Product-scoped `.agents/products/<product>/features/*.md` for something Feature-specific, or
  the file that already owns the concept (Goals mechanics belong with the Goals catalog, Roadmap
  mechanics belong with the Roadmap catalog). Create the target doc if none fits yet -- do not
  invent a new mechanism for this.
- Replace the moved text in `AGENTS.md` with the rule statement plus a link.

Before proposing the move to a human, diff old against new to confirm nothing was dropped,
only relocated, and confirm the file's word count actually fell. This is a `CUSTOMIZE_ONCE`
file the project owns -- propose the plan and get confirmation before restructuring it, the
same as any other "next steps" notice (see `AGENTS.md`'s Session Start section).

A rule statement that still runs long after this exercise is worth a second look: it may
actually be two rules wearing one ID.

## Context Budgeting

**Context Budgeting** assigns limits according to how content reaches an agent, not according to
its file extension. Content automatically or routinely supplied to an agent is
**Context-budgeted**. Content retrieved only after a relevant task or trigger is **Reference**.
Reference does not mean unlimited: it still needs one owner, useful structure, and no accidental
duplication, but it does not pay a fixed cost in unrelated sessions.

Some artifacts are hybrid. A skill's discovery metadata is Context-budgeted while its `SKILL.md`
body and references are on demand. A Goals or Venture catalog may be Reference while a generated
summary is Context-budgeted. Budget and measure only the fragment the agent actually receives.

The high-frequency context surfaces in agent-base are:

| Surface | Budgeted part |
| --- | --- |
| Always-on instructions | Agent-base-owned content rendered into downstream `AGENTS.md` |
| Vendor adapters | The adapter and injected hook text received by one vendor |
| Venture and Goals | Project-owned content inside the composed startup summary |
| Roadmap | The index or status material required at task start |
| Skills | Aggregate discovery metadata; bodies load only after activation |

The rendered result is authoritative for measurement. Source files can understate cost when blocks
are injected or summaries are generated, and summing every adapter overstates it because one agent
receives only its own adapter. For downstream sessions, `template/AGENTS.md` may use 2,500 words
of static agent-base content. The complete agent-base-owned payload stays within 2,500 words of
the 5,000-word combined budget, leaving 2,500 for project-owned context, including generated
Identity, Strategy, and Objective text. The renderer keeps those canonical entities whole; the
composed gauge, not an independent field cap, surfaces an over-budget project.

Budgeted content should contain the minimum complete routing contract: the condition that makes the
topic relevant, an explicit mandatory-load instruction with a canonical path, and any critical
boundary that must be known before the detail can safely be loaded. Conditional procedure,
rationale, examples, and history belong behind that trigger. Never move the highest-stakes clause
of a multi-clause safety, authority, or destructive-action rule behind a late pointer.

This design follows progressive-disclosure guidance from the
[Agent Skills specification](https://agentskills.io/specification), concise and scoped instruction
guidance from [Claude Code](https://code.claude.com/docs/en/memory) and
[GitHub Copilot](https://docs.github.com/en/copilot/concepts/prompting/response-customization), and
OpenAI's recommendation to remove repeated prompt content incrementally and rerun representative
evaluations. Local roadmap item 036 provides the stricter timing mitigation: its mandatory-load
trigger fired in every trial, but the highest-stakes clause still needed to remain inline.

Use the `context-budgeting` skill before adding, expanding, generating, or restructuring any of
these automatically supplied surfaces. The skill owns the editing and verification procedure; this
standard owns the classification and policy.

## Agree the design before material implementation (AB-DESIGN-001)

Before an implementation that has a genuinely different design, performs an external write,
or creates a lasting public artifact, first establish that the human agrees with its rough
principle and scope. Do the read-only investigation needed to understand the facts, then stop
before the first mutation and present:

- verified facts and the decision still open;
- the recommended principle and scope, including affected systems;
- meaningful alternatives and tradeoffs when they exist; and
- the external, persistent, or difficult-to-reverse effects.

Wait for an explicit approval of that proposal. Do not treat a multi-stage request such as
"propose it and then do it" as approval of an implementation that has not yet been proposed.
Approval covers only the stated principle and scope; if evidence changes either materially,
return to this gate.

The gate is not a request to re-approve ordinary work. Once the approach is approved, complete
the coherent work block through ordinary investigation, edits, checks, and fixes without interim
progress narration. Return only when the block is complete or a real boundary remains: an
unapproved irreversible or external action, missing authority or essential domain knowledge, a
materially different valid choice, or a technical blocker after reasonable repair. Nor does it
block read-only diagnosis or a directly specified, mechanically determined edit with no meaningful
design choice.

Persistent names and shapes -- database tables or columns, API paths, public interfaces, and
similar artifacts -- always trigger the gate. Present at least two real options with tradeoffs
and a recommendation for those decisions, because changing them after deployment is costly.

## Overriding an agent-base default

The `### Agent-base Rules` block in `AGENTS.md` states universal defaults, not fixed law. A
project may diverge from one -- explicitly, never silently. This only applies to rules in
that block; a project's own rules are already project-owned and just get edited directly, no
override ceremony needed.

Overrides live in `AGENTS.md`'s `## Overrides` section, just below the block -- never inside
the block itself, which the next sync replaces wholesale and would silently erase an override
written there. Adding one is an `AB-DESIGN-001` decision (it creates policy that outlives this
conversation): propose it, wait for confirmation, same as any other persistent artifact.

Agent-base-owned defaults always use `AB-*` IDs. An unprefixed rule ID outside the managed
block is project-owned and must never be renamed or deleted merely because its suffix matches
an agent-base default. Releases through v0.0.96 used unprefixed IDs for the defaults; when
reading an old reference, interpret it as the corresponding `AB-*` default only when its
source clearly predates v0.0.97 and no project-owned rule claims that ID. New overrides always
target the `AB-*` ID.

Format, one entry per override:

```markdown
### Override: AB-GIT-001

**Rationale**: this team allows agents to commit directly to feature branches; a human still
reviews before merge to main.
**Scope**: feature/* branches only.
**Approved by**: human, 2026-08-25.
```

Two asymmetric cases:

- **Strengthening** a default (a stricter rule than the agent-base baseline, e.g. "never touch
  `payments/` without two-person review") needs only a rationale -- it cannot make the project
  less safe than the default already is.
- **Relaxing** a default (permitting something the default forbids, e.g. the `AB-GIT-001` example
  above) additionally needs the `**Approved by**: human, <date>` line filled in with an actual
  human, not an agent, and a rationale that addresses the specific risk being accepted, not
  just convenience.

An agent applying a rule from the block checks `## Overrides` first for a matching entry; if
one exists, the override governs, not the block's default. This does not need a validator to
be worth doing -- a human reading `## Overrides` alongside the block sees every place this
project's real policy differs from agent-base's, in one place, instead of it being scattered
or, worse, silently contradicted somewhere else in the file.

## Cross-references

- Behavior rules and rule IDs: see `AGENTS.md`
- Formatting: see `.agents/standards/formatting.md`
- Goals mechanics (`AB-GOALS-001`): see `.agents/goals/_GOALS_CATALOG.md`
- Roadmap mechanics (`AB-ROADMAP-002` through `AB-ROADMAP-004`): see
  `.agents/roadmap/_ROADMAP_CATALOG.md`
