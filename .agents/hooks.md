# Agent Hooks

Agent-base hook definitions. Vendor adapters are generated from these during sync.

Do not add project-specific hooks here - this file is kept in sync with the agent-base.
Projects can add their own hooks directly in their adapter files, outside the
`AGENT-BASE-HOOKS` markers.

## on-session-start

Commands that must run at the beginning of every agent session:

```bash
bash "$(git rev-parse --show-toplevel)/agent-base-guard.sh"
```

## on-every-change

Read and follow `AGENTS.md` in full; it is the canonical behavior contract.

## on-task-start

Begin the first user-visible response of each session with the banner specified by
`AB-SESSION-002` in `AGENTS.md`; show it once.

After that banner, render the Agent-base Board and first-response SESSION introduction required by
`AB-RESPONSE-001` in `AGENTS.md`.

Before substantive work, make sure you understand the Venture Context, Goals, current Roadmap,
and applicable human-authority and safety boundaries; load the standards and skills routed by
`AGENTS.md`.
