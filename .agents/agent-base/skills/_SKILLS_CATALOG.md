# Agent-base Skills Catalog

Canonical index of generic procedures owned and kept in sync by agent-base. Project-owned
skills remain in `.agents/skills/`; never add project content here.

## Skills

| Skill | Description |
| --- | --- |
| [ab-quality-check](./ab-quality-check/SKILL.md) | Review this repository against the Agent Base quality conditions and report findings; never updates |
| [agent-base-sync](./agent-base-sync/SKILL.md) | Force an immediate mid-session agent-base sync and report drift or migrations clearly |
| [context-budgeting](./context-budgeting/SKILL.md) | Safely budget automatically supplied agent context using measured rendering, mandatory-load triggers, and behavior-first verification |
| [create-venture](./create-venture/SKILL.md) | Guide a human and agent from an empty repository through the first verified Venture foundation |
| [create-worktrees-by-convention](./create-worktrees-by-convention/SKILL.md) | Explicitly create a project's declared set of worktree slots after a strict Git preflight |
| [decision-refinement](./decision-refinement/SKILL.md) | Separate analysis from open decisions, plan them, and resolve them one at a time |
| [executor-reviewer-loop](./executor-reviewer-loop/SKILL.md) | Run a bounded cross-vendor Executor/Reviewer review loop (a.k.a. Actor-Critic) on one task until convergence or a round cap |
| [venture-staffing](./venture-staffing/SKILL.md) | Staff a coherent work unit with the cheapest sufficiently capable Actor and hold the write, recording, and recovery boundaries |
| [venture-discovery](./venture-discovery/SKILL.md) | Elicit Venture intent from the responsible human before structuring Purpose, Vision, Strategy, or Goals |
| [venture-review](./venture-review/SKILL.md) | Periodically detect internal Venture hierarchy incoherence and route human-approved repair to discovery |
| [venture-wiki-sync](./venture-wiki-sync/SKILL.md) | Inspect and reconcile canonical Venture documents with deterministic wiki mirrors |

## Rules

- Agent-base-owned skills use `.agents/agent-base/skills/skill-name/SKILL.md`.
- Project-owned skills use `.agents/skills/skill-name/SKILL.md`.
- Files in this directory are `KEEP_IN_SYNC`; downstream projects never customize them.
