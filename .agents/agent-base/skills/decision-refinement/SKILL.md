---
name: decision-refinement
description: Separate analysis from open decisions, plan the decisions, then resolve them one at a time. Use whenever a response would carry two or more open decisions whose different answers lead to materially different work.
---

# Decision Refinement

Long answers hide decisions. When analysis and open questions share one block of prose, the human
has to read all of it to find what is being asked of them, and a buried question gets answered by
accident or not at all. Keep the two apart: the analysis is a deliverable, the decisions are a
queue.

## When this applies

Two or more open decisions in one response, where different answers lead to materially different
work. Ask a single decision directly, without ceremony.

A decision with a safe default is not a decision. State the assumption and continue. The queue
holds only what genuinely needs the human, which is how this stays compatible with `AB-HUMAN-001`
rather than becoming a licence to ask more.

## Separate the analysis

Write the analysis once and put no question in it. It should read as a finished statement of what
is true, including the findings a decision depends on. If it cannot be written without asking
something first, that missing fact is the first queue item, not a parenthesis in the prose.

## Plan the decisions

List every open decision, numbered, one line each: what is being decided, why it matters, and the
recommended default. Order by dependency first, so a decision that changes the others comes first,
then by blast radius.

Show the plan once before asking anything. It is what lets the human see the whole shape, reorder
it, drop items, or answer several at once. Skipping straight to questions hides the size of what
is being asked.

## Ask one at a time

Then ask one decision per turn. Each question carries a short title, one sentence on what is at
stake, and two to four concrete options with their consequences, recommended one first. Do not
restate the analysis inside the question.

Use the vendor's structured question mechanism when it has one; otherwise number the options in
plain text. Record each answer back into the plan before asking the next, so the resolved set
survives compaction and the human sees progress without scrolling.

Independent, cheap decisions may be offered together only when each keeps its own title and
options. Merging unrelated decisions into one paragraph is the failure this skill exists to
prevent.

## Finish

When the queue is empty, restate the resolved set compactly and proceed. A decision discovered
during the work joins the plan; it does not get folded into a progress report.

Never use the queue to defer a safety, destructive-action, or external-write confirmation, and
never treat a long queue as a reason to act without an answer.
