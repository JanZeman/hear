# Human Interruptions

Canonical contract for the questions agents put to humans: what shape they take, and how they are
measured. Agent Autonomy removed most permission dialogs, so the remaining cost is
decision quality, not dialog count. Measurement stays; it is no longer the point.

## Agent behavior (`AB-HUMAN-001`)

A question the human cannot find is not an answered question. Shape every question so it can be
answered directly. With two or more open decisions, load
`.agents/agent-base/skills/decision-refinement/SKILL.md` first and follow it:
analysis carries no questions, the open decisions become one numbered plan, and they are asked one
at a time with concrete options. Do not bury a decision in explanatory prose, and do not present a
wall of analysis whose real purpose is to obtain an answer.

Never trade question count for safety. Reducing prompts never widens scope, applies a permission
automatically, hides a destructive or external write, or bypasses a required safety decision.

This rule applies to root agents and subagents. A subagent should return a blocker to its parent
when the parent can resolve it; it should not independently ask the human for the same decision.

## Visibility and repair (`AB-HUMAN-002`)

Successful automated checks, including collected telemetry, automatic reviews, and repeated
permission patterns, produce no conversation output. Inspect their aggregates only through an
explicit `report` or `recommend` request.

On a collector or check failure, surface one concise agent-facing diagnostic. In agent-base,
diagnose and repair the failure when it is safe and in scope; otherwise create or update the
relevant roadmap item. In a downstream client, do not guess at an upstream repair: prepare a
copy-paste prompt for an agent-base agent with the visible failure and relevant checks.

## Counting model

- **Human request**: one distinct question, approval, confirmation, or requested human action.
- **Blocking interruption**: a request without whose answer the current work cannot continue.
- **Human round trip**: one agent response or vendor dialog followed by one human response. Several
  questions answered together are several requests but one round trip.
- **System approval**: a vendor-generated permission dialog for a tool, command, file, network, or
  connector action.
- **Clarification**: missing task information requested in ordinary agent text.
- **Policy confirmation**: ordinary agent text asking whether a proposed action is authorized.
- **Action request**: ordinary agent text asking the human to perform work before the agent can
  continue.
- **Handoff**: a non-blocking request after the agent's work is complete, such as review or commit.
- **Aborted / automatically resolved**: a pending request ended without a human answer, or resolved
  by the vendor or another policy mechanism.

Every event carries schema version, timestamp, vendor, anonymized session/turn/item/workflow,
root-or-subagent scope, anonymized repository, category, blocking flag, outcome, wait time, round
trip ID, normalized signature, source, and confidence. Raw prompts, assistant messages, command
arguments, paths, URLs, tool results, and secrets are never written to telemetry.

## Accuracy boundaries

| Source | Coverage | Accuracy label |
| --- | --- | --- |
| Codex `PermissionRequest` hook | A permission dialog is about to appear | Authoritative request; outcome inferred from later tool hooks |
| Claude `PermissionRequest` hook | A permission dialog is about to appear | Authoritative request; outcome inferred from later tool hooks |
| Codex App Server JSON-RPC adapter | Thread, turn, item, request response, and resolution in custom hosts | Authoritative for the messages supplied to the adapter |
| `Stop` / `SubagentStop` message classifier | Ordinary questions, confirmations, action requests, handoffs | Heuristic; displayed separately from authoritative sources |
| Explicit `record` adapter | Vendor or host integrations not covered above | Adapter-reported |

Codex and Claude hooks expose session/subagent context, but only Codex exposes a stable turn ID in
the hook payload. The collector creates a local per-session turn key for Claude. Neither vendor's
machine-wide `PermissionRequest` hook exposes the final dialog answer directly; `PostToolUse`
confirms execution, while a session ending with an unresolved request is recorded as aborted.
Custom Codex App Server hosts can feed a JSONL copy of protocol traffic to
`ingest-codex-app-server` for request/response correlation. Built-in clients are not claimed to do
this automatically. A client response counts as a human round trip only when that host explicitly
uses `--client-responses-are-human`; otherwise it remains a client response, not an asserted human
action. Machine-hook approvals always count as blocking interruptions, but not confirmed human
round trips, because another concurrent policy hook may have resolved them.

Official references researched for this implementation:

- [Codex hooks](https://learn.chatgpt.com/docs/hooks)
- [Codex App Server approvals](https://learn.chatgpt.com/docs/app-server#approvals)
- [Codex command rules](https://learn.chatgpt.com/docs/agent-configuration/rules)
- [Claude Code hooks](https://code.claude.com/docs/en/hooks)
- [Claude Code permissions](https://code.claude.com/docs/en/permissions)

## Local state and retention

The collector is machine-wide and repository-independent. Defaults:

- configuration: `${XDG_CONFIG_HOME:-$HOME/.config}/agent-base/human-interruptions/config.json`
- events: `${XDG_STATE_HOME:-$HOME/.local/state}/agent-base/human-interruptions/events.jsonl`;
  installation records the resolved state directory and falls back to the private agent-base data
  directory when the standard state root is not writable
- bounded runtime watermarks: `<resolved-state-directory>/runtime.json`
- installed collector: `${XDG_DATA_HOME:-$HOME/.local/share}/agent-base/human-interruptions.py`
- directory mode `0700`, file mode `0600`, rolling retention 30 days

Repository, session, turn, item, workflow, and agent identifiers are salted hashes. The random salt
stays in the private local configuration. Deleting it intentionally breaks cross-install
correlation.

## Explicit machine-wide lifecycle

Repository sync distributes the script, behavior rule, and narrow reporting permissions. It never
enables telemetry or grants collector lifecycle commands. From a synced repository, the human
explicitly runs:

```bash
python3 scripts/human-interruptions.py install --vendor all
python3 scripts/human-interruptions.py status
python3 scripts/human-interruptions.py update --vendor all
python3 scripts/human-interruptions.py disable
python3 scripts/human-interruptions.py uninstall --vendor all
```

`install`/`update` copy the collector to operator-local storage and add owned hooks to
`~/.codex/hooks.json` and `~/.claude/settings.json` (or `$CLAUDE_CONFIG_DIR/settings.json`). They
preserve unrelated settings and are idempotent. `disable` leaves hooks and data for audit but makes
collection a no-op. A partial `uninstall` removes only the selected vendor's hooks and keeps the
shared collector, configuration, and data for remaining vendors. The installed script is removed
only after every vendor hook is gone; `--purge-data` takes effect only then. No lifecycle command
adds or approves a permission.

Sync may merge three narrow project-local Claude permissions for `status`, `report`, and
`recommend`. The policy also removes exact obsolete agent-base entries, including the former
blanket collector rule, while preserving every unrelated project permission.

## Reports and recommendations

```bash
python3 scripts/human-interruptions.py report --days 7
python3 scripts/human-interruptions.py report --days 30 --recommendations
python3 scripts/human-interruptions.py recommend --days 30 --minimum 2
```

Reports include request and round-trip totals, category/outcome counts, and aggregates by vendor,
anonymized repository, and root/subagent scope. Hooks are silent when collection succeeds.

`status`, `report`, and `recommend` are read-only. They neither create missing telemetry state nor
rewrite retention data; collection hooks perform bounded retention when they append events.

Recommendations are evidence for `/permission-promote`, never a second permission owner. Before
proposing a command rule, that workflow verifies the sandbox-first baseline in
`agent-autonomy.md`; an already sandboxed command, a narrow filesystem/domain gap, or an Auto-mode
precedence issue must not become a broad allowlist. The tool proposes only repeated, normalized,
known-low-risk prefixes and refuses mutating Git, destructive, network, credential-bearing,
compound-shell, or unknown commands when no narrow stable resolution exists. Every proposal states
vendor syntax, persistent scope, occurrence count, expected reduction, and security tradeoff. A
human must review and apply it through the vendor's normal permission workflow.

Repeated approval signatures are analyzed only when a human explicitly runs `report` or
`recommend`. Nothing is applied; `/permission-promote` remains the review path.

Copilot and Warp are intentionally unsupported in this first version. Add an adapter only after an
official event source can be tested; do not label generic transcript guessing as vendor support.
