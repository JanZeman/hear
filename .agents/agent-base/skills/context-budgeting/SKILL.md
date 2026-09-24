---
name: context-budgeting
description: Measure, add, compress, or restructure content automatically supplied to agents without weakening behavior. Use for AGENTS.md, adapter or hook instructions, generated context summaries, startup catalogs, and skill-discovery metadata; not for ordinary on-demand reference prose.
---

# Context Budgeting

Optimize the context an agent receives repeatedly. The budget follows the rendered context surface
or loaded fragment, not Markdown as a format. Preserve behavior first; fewer words are useful only
when the agent still notices the same condition, loads the right detail in time, and respects the
same boundary.

Read `.agents/agent-base/standards/documentation.md` for the canonical Context-budgeted versus
Reference model and repository limits before editing. In the standalone agent-base source, use
`template/.agents/agent-base/standards/documentation.md`, the upstream source of that file.

## Establish the budget surface

Identify all of these before changing text:

- the canonical source and every rendered or injected destination;
- which agents receive it, how often, and whether loading is automatic, task-start, or on demand;
- the owned range that may be changed without touching project content;
- the current source and rendered word counts;
- the hard ceiling, working target, and content generated only at runtime.

Measure the effective result, not just its source. A managed block, adapter injection, summary, or
metadata field spends context where the agent receives it even when it lives elsewhere in the
repository. If no budget exists, report the baseline and propose one from loading frequency and
blast radius; do not invent a new hard limit silently.

## Classify before compressing

Assign each relevant statement one role:

1. **Critical invariant**: must be known before the agent can safely load more context or act.
2. **Routing trigger**: makes the agent recognize when and where to load detail.
3. **Conditional procedure**: needed only after the trigger fires.
4. **Rationale, example, or history**: supports maintenance but does not direct the immediate act.
5. **Duplicate**: restates a canonical owner without adding a distinct obligation.

Keep critical invariants and routing triggers in the budgeted surface. Move conditional procedure
and explanatory material to one natural Reference owner. Remove a duplicate only after confirming
that its canonical owner remains reachable. Do not hide a frequent, short rule behind a pointer
when loading the reference would cost more and reduce clarity.

## Write a reliable trigger

A pointer such as `Details: file.md` is not a trigger. State:

- the observable condition;
- `load` or `read` and the exact canonical path;
- `first`, `unconditionally`, or an equally explicit timing boundary when timing matters;
- the action that must not begin before loading; and
- the highest-stakes clause inline for a multi-clause safety, authority, or destructive-action rule.

Prefer this shape:

> Before `<condition/action>`, load `<canonical path>` first; `<critical inline boundary>`.

Do not rely on a deep reference chain. The first loaded document should contain the operative
procedure or one clearly routed next reference. Imports that load at startup improve organization
but do not save context.

## Preserve reliability

Reliability outranks brevity in every decision this skill governs. When a shorter wording and a
clearer one conflict, take the clearer one and fund it by removing redundancy elsewhere; never buy
words by making a sentence harder to read.

Compression has two distinct failure modes, and the ledger below catches only the first:

| Failure | Question | Caught by |
| --- | --- | --- |
| Lost obligation | Did something required disappear? | The ledger |
| Lost comprehensibility | Is the surviving sentence still ordinary readable prose? | Reading it aloud |

The second is the dangerous one, because every check still passes. Words get squeezed out until the
grammar breaks and the sentence becomes a telegram that parses only for whoever wrote it. An
instruction nobody can read is not a cheaper instruction; it is an absent one, and it fails silently
because the text is still there. After compressing any sentence, read it as English: if a competent
reader would have to decode rather than read it, it is too short regardless of what the ledger says.

Create a small semantic ledger in the task notes or roadmap item before rewriting:

| Existing obligation | Inline after edit | Canonical detail | Trigger |
| --- | --- | --- | --- |

Account for every rule ID, priority, prohibition, stop condition, approval boundary, human-authority
clause, exception, and required verification. Compress one coherent group at a time. If a statement
cannot be mapped confidently, leave it inline and report it rather than guessing.

Never modify project-owned text merely to meet an agent-base budget. For Customize-once files,
change only an explicitly managed range or propose a reviewed migration that matches an exact known
legacy shape. A shorter file does not justify weakening ownership boundaries.

Never shorten a canonical Venture entity in its generated always-on block. Identity, Strategy, and
every Objective remain complete; the composed gauge reports excess so the human can improve the
source or fund it deliberately. Purpose, Vision, and Key Results may stay behind explicit triggers
because they are conditionally loaded rather than partial renderings of the same entity.

## Verify the result

At minimum:

1. Diff old and new obligations using the semantic ledger.
2. Measure canonical sources and maximum rendered context, including generated summaries.
3. Confirm every mandatory path exists and is distributed to the consumer before its trigger.
4. Test realistic scenarios: the trigger must fire, detail must load before action, and the outcome
   must preserve the full-inline behavior. Include the highest-risk multi-clause case.
5. Verify project-owned text outside managed ranges is byte-for-byte preserved.
6. Run relevant lint, syntax, propagation, and downstream rehearsal checks.

For agent-base, `template/AGENTS.md` holds at most 2,500 words plus the generated Venture reserve,
which `venture.py` derives from the block's own parts. The complete agent-base-owned session
payload stays within 2,500 words of the 5,000-word combined budget, leaving 2,500 for the project.
Restating the derived reserve is how earlier enforced and documented ceilings diverged. A lower
working target creates feature headroom. Do not raise a limit merely to make a change pass.

Report the before/after counts, what moved, the behavioral evidence, and any content deliberately
left inline. If behavior cannot be demonstrated reliably, prefer the larger safe version.
