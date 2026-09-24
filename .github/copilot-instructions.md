# Copilot Instructions

Copilot adapter for this repository.

<!-- AGENT-BASE-HOOKS:START -->
## Session Start - MANDATORY

Run immediately without asking for confirmation:

```bash
AGENT_BASE_VENDOR=copilot bash "$(git rev-parse --show-toplevel)/agent-base-guard.sh"
```

Follow its output. If the guard fails, stop and ask the human before continuing.

## Every Change - MANDATORY

Read and follow `AGENTS.md` in full; it is the canonical behavior contract.

## Task Start - MANDATORY

Begin the first user-visible response of each session with the banner specified by
`AB-SESSION-002` in `AGENTS.md`; show it once.

After that banner, render the Agent-base Board and first-response SESSION introduction required by
`AB-RESPONSE-001` in `AGENTS.md`.

Before substantive work, make sure you understand the Venture Context, Goals, current Roadmap,
and applicable human-authority and safety boundaries; load the standards and skills routed by
`AGENTS.md`.
<!-- AGENT-BASE-HOOKS:END -->

Keep only vendor-specific Copilot content outside this managed block.
