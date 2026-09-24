# Session Continuity

## Contract (`AB-SESSION-001`)

For substantive work, maintain agent-authored handoff snapshots in `.agents/sessions/`. Save one
on an explicit request, before known context compaction, and before handing work to another session,
vendor, or human. Short answers and trivial sessions need none.

Use the save timestamp and a slug inferred from the actual work:
`YYMMDD-HHMM-kebab-name.md`, for example `260830-1415-session-continuity.md`. Tell the human the
name after creating it; do not ask them to name an empty session up front.
Prefer a precise task identity over a shorter slug: a directory names the category, not the work.
For example, use `session-management`, not the ambiguous `management`.

Every save creates a new file; never rewrite an earlier snapshot. A continuing snapshot names its
immediate predecessor by basename on one exact metadata line:

```markdown
**Continues**: `260830-1415-session-continuity.md`
```

State the current task, completed work, decisions and constraints, changed files, verification,
open risks, and concrete next steps. Keep it a concise handoff, never a raw transcript or a store
for secrets.

After creating a continuing snapshot, run
`python3 template/scripts/session-snapshots.py .agents/sessions/<new-snapshot>.md`. The helper automatically
prunes a provably linear lineage to its newest two files. Briefly tell the human what it removed;
do not ask permission. Git review exposes the deletion and history keeps it recoverable. The helper
must refuse branches, cycles, unsafe paths, or invalid metadata. If it refuses, keep every file and
report the ambiguity; never infer lineage from timestamps, topics, or filename similarity.

These files are project knowledge: do not gitignore them. Leave snapshots and pruning deletions for
normal human review, staging, and commit under the repository's Git rules. A session-end hook may
record deterministic metadata, but it cannot substitute for an agent-authored summary.

## Conversation focus, queue, and detour stack

A queue and a stack are different structures and both are needed. The **queue** holds unrelated
things the human raised, handled later in order. The **stack** holds detours: work entered because
the work below it could not proceed, unwound in the opposite order. Losing the stack is how an
agent finishes a detour and never returns to what caused it.

Every substantive handoff has these headings:

```markdown
## Active topic

## Queued topics
```

Hold one active topic per conversational round. A second topic is allowed only when it directly
depends on resolving the first; state that dependency. When one user message contains unrelated
questions or requests, select the first active topic and add the rest to `Queued topics` with a
short, precise description. Do not forget or silently discard a queued item.

Before queueing an item, decide whether it is broad, important, or independent enough for its own
roadmap item. Create or update that roadmap item instead of leaving such work only in a session
queue. When deferring a queued item in conversation, say in the user's language that it will be
handled later and name the active topic now being addressed. For example: “We will return to that
later. Now we are focusing on this: …”

On completing an active topic, choose the next queued item deliberately, promote it to a roadmap
item when warranted, or remove it only after resolving it. The guard warns when an existing
handoff lacks either required heading; it cannot decide whether topics are semantically related.

### Detour stack

When a detour is open, add a third heading and write the return path, deepest first:

```markdown
## Detour stack

1. <the detour being worked on now>
2. <what it interrupted, and exactly where that stood>
3. <what that interrupted, and exactly where it stood>
```

Include it only while something is actually suspended, and delete the heading once the stack is
empty. The guard deliberately does not require it: a heading demanded of every handoff would
appear in files that never had a detour, and a warning that fires on correct work teaches the
reader to skip warnings.

Record where each suspended item stood, not just its name. "Waiting on the fleet rehearsal" is a
resumable note; the bare title of a task is not, and by the time anyone reads the stack the
context that made it obvious is gone.

Announce both ends of a detour. Entering one is already covered above. Leaving one is not, and it
is the half that gets skipped: name what you are returning to and where it stood, before
continuing it. An unannounced return looks identical to a topic change.

A detour that crosses into agent-base becomes a request under `.agents/requests/`; see
`AB-REQUEST-001` and that directory's catalog for how the answer finds its way back.
