# ai-venture Standards Catalog

ai-venture's own canonical reference docs - not project content, not editable here. Kept in
sync automatically; edit upstream in ai-venture itself if something needs to change. Separate
from the project's own `.agents/standards/`, which is project-owned and never touched by this
sync.

| Standard | Description |
| --- | --- |
| [documentation.md](./documentation.md) | Context Budgeting and lean `AGENTS.md` (`DOC-003`, `AV-CONTEXT-001`); design approval (`AV-DESIGN-001`) |
| [fix-log.md](./fix-log.md) | Prompt repair, concise verified fix records, and on-demand reuse of prior fixes |
| [secrets.md](./secrets.md) | When credentials may live in tracked files (`AV-SAFE-001`) |
| [git.md](./git.md) | Commit language, allowed git commands, commit suggestions (`AV-DOC-005`, `AV-GIT-001`, `AV-GIT-002`) |
| [agent-autonomy.md](./agent-autonomy.md) | Sandbox-first Codex/Claude defaults, global installation, safety boundaries, and diagnostics |
| [human-interruptions.md](./human-interruptions.md) | Counting, privacy, vendor coverage, installation, and safe recommendations (`AV-HUMAN-001`) |
| [agent-correspondence.md](./agent-correspondence.md) | Addressed, dated, immutable messages between agents on expensive-to-reverse work; roles as addresses, derived turn |
| [session-continuity.md](./session-continuity.md) | Immutable linked handoffs and safe two-snapshot retention (`AV-SESSION-001`) |
| [roadmap-workflow.md](./roadmap-workflow.md) | Goals alignment, task-start checks, status vocabulary (`AV-GOALS-001`, `AV-ROADMAP-001`, `AV-ROADMAP-004`) |
| [venture-operating-model.md](./venture-operating-model.md) | Venture context, Categories, Departments, routing, and repository authority |
| [wiki-visibility.md](./wiki-visibility.md) | Operator-local host/path classification before wiki writes (`AV-WIKI-002`) |
| [rule-writing.md](./rule-writing.md) | The eight-step process and canonical forms for writing or rewriting any `AGENTS.md` rule |
| [vendor-hooks.md](./vendor-hooks.md) | Abstract hook points (session start, every turn, before a tool runs) and their concrete Claude Code/Codex implementation |
| [vendor-support.md](./vendor-support.md) | Vendor support tiers, local priority profile, and release coverage (`AV-VENDOR-001`) |
| [model-routing.md](./model-routing.md) | Tool-first model routing, bounded LOW packets, route receipts, and Primary Claude coverage (`AV-MODEL-001`) |
| [venture-staffing.md](./venture-staffing.md) | Staffing a work unit with the cheapest sufficient Actor: capability ladder, write lease, invocation contract, recovery (`AV-MODEL-001`) |
| [mcp-credential-diagnostics.md](./mcp-credential-diagnostics.md) | Staged, secret-safe MCP connection diagnostics for fresh and managed hosts (`AV-MCP-001`) |

Plain `kebab-name.md` filenames, no numeric prefix - this directory is small and grows rarely;
a stable descriptive name is enough.
