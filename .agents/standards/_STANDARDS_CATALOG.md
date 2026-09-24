# Standards Catalog

Canonical index of project standards. All standards docs live in this directory. Agent-base's
own canonical reference docs (not project-specific) live separately in
`.agents/agent-base/standards/` and are not listed here.

## Standards

| Standard | Description |
| --- | --- |
| [architecture.md](./architecture.md) | Layers, dependency rules, folder structure |
| [development-workflow.md](./development-workflow.md) | Trunk-based branching, two-letter repo_id, and the worktree slot convention |
| [formatting.md](./formatting.md) | Code and Markdown formatting tools and rules |

<!-- TODO: Add your project-specific standards here too, as you fill them in or add new ones. -->

## Rules

- One doc per distinct standard.
- Plain `kebab-name.md` filenames - no numeric prefix. This directory is a flat, growing
  catalog, not a sequence; a stable descriptive name is a better permanent identifier than an
  arbitrary number, and avoids collisions with agent-base's own numbering choices.
- This catalog is the single source of truth for the standards list.
- New standards are added here before being referenced in `AGENTS.md`.
