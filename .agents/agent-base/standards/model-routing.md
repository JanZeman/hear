# Model Routing

Use a deterministic local tool first whenever it can produce the required result. File search,
exact comparison, formatting, and an available project check do not justify a model launch.

When model work remains, classify the route before acting:

| Route | Use when | Evidence |
| --- | --- | --- |
| `tool` | A local deterministic tool can finish the work. | Tool output or check result. |
| `low-helper` | The task is bounded LOW work and a configured native helper can execute it. | Native start and stop receipt. |
| `parent` | The task needs normal implementation, broader judgment, or a new permission. | Parent verification. |
| `blocked` | The task handles credentials, a vulnerability, authentication, authorization, personal data, or a new external access path. | Human decision. |

## Staffing

The table above decides one hand-off. When work spans more than one coherent WORKLOAD or more than
one WORKER, the questions that cost money stop being "which MODEL" and become "who may write", "what was
recorded before it ran", and "did anyone observe the result". Load
`.agents/agent-base/standards/venture-staffing.md` first, then its
`venture-staffing` skill for the procedure.

Two things from that contract belong here, because they change how this table is read. `blocked`
is the `human` route and is not a capability tier, so it is unreachable by escalation. And the order
is authority, then eligibility, then economics: `tool` is an economic answer, not a permission, so a
deterministic operation on a credential still reaches the human.

## LOW packet

Before launching a helper, the parent writes a packet with all five fields: allowed data or files,
expected result, prohibited scope, helper self-check, and parent verification. It announces the
actual helper launch immediately before starting it and reports the parent's verification after it
returns. A helper may write when the packet and current permissions allow it; a task requiring a
new permission returns to the parent.

## Primary Claude v1

Claude's `ab-low-helper` is a native Haiku background subagent. Its project lifecycle hooks write
content-free start and stop receipts under the local state directory. A receipt records only the
vendor, event type, time, and one-way identifiers needed to pair the start with the stop. It never
stores the task, prompt, files, transcript, or helper response.

The helper's configured model plus a matching start and stop receipt proves that this specific
helper ran. It does not prove that Claude would select the helper for every suitable task. That
claim requires a paid Behavioral Test whose LOW fixture fails when either receipt is absent.

Run `python3 scripts/route-receipts.py status` to inspect only aggregate local counts. The receipt
mechanism is observational: it never blocks a subagent or changes permissions.

## Primary Codex v1

Codex's `ab-low-helper` is a native Luna subagent with low reasoning. Its project lifecycle hooks
write the same content-free start and stop receipts as Claude's helper. Codex requires JSON output
from `SubagentStop`, so the receipt helper returns an empty object after recording that event.

The helper inherits the parent turn's sandbox and permission mode. The parent therefore decides
whether its bounded packet may write. A matching start and stop receipt proves that this specific
helper ran; it does not prove that Codex would select it for every suitable task.

## The model ladder (AB-COST-001)

Which model sits on which rung is an operator's fact with a date on it, kept in
`~/.config/agent-base/model-ladder.json` beside `vendor-support.json` and `staffing.json`. Model
names and prices change monthly and depend on a subscription, so a default shipped in agent-base
would teach every client an answer that is wrong by the next release.

```json
{
  "version": 2,
  "reviewed": "2026-09-19",
  "review_after_days": 90,
  "vendors": {
    "<vendor>": {
      "<model-id>": {"rung": "cheap|standard|strong|expert", "relative_cost": 1,
                     "aliases": ["<what the CLI accepts>"],
                     "efforts": {"<effort>": {"relative_burn": 1, "samples": 5}}}
    }
  }
}
```

`aliases` names what the vendor's own CLI accepts. The two files that name models are written for
different readers: this one records the versioned identifier a vendor publishes, while `staffing.json`
records what an operator types into a command line. Without the aliases the same model under its two
names reads as two, one of them unrecorded, and the cross-check reports a disagreement that is only a
spelling.

The rungs are the capability ladder's own, so there is one vocabulary rather than two. Cost is a
ratio against the cheapest model recorded, never a price: what an operator pays depends on a plan
that may not bill per token at all, while "roughly twelve times" stays true long enough to route by.

Three things read it and none keeps its own copy. `scripts/check-model-cost.sh` runs at session
start and names the expensive models, so a session on one says so before it spends; it warns and
never refuses, because an expensive model is often the right choice and a check that blocks gets
turned off. `staffing.json` is cross-checked against it, since the two answer different questions,
the model an operator *chose* for a tier and the rung a model *is* on, and only the operator knows
which is out of date when they disagree. And a ladder past its review says how far past, because a
stale answer given confidently is worse than none.

Absent, all of this is silent: a client that has not adopted it behaves exactly as before.

`scripts/model-ladder.py --check`, `--rung <vendor> <model>`, and `--expensive` inspect it.
`--modelmodes` orders known MODELMODE costs. `--capability-evidence` reports verified outcomes by
MODELMODE and required capability, but never turns sparse outcomes into a made-up capability rank.
