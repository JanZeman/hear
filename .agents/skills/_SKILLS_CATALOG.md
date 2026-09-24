# Skills Catalog

Canonical index of skills. Each skill is a folder in `.agents/skills/` containing a `SKILL.md`
file with YAML frontmatter and full instructions, following the
[Agent Skills](https://agentskills.io/specification) open format.

## Skills

| Skill | Description |
| ----- | ----------- |
| [wiki-edit](./wiki-edit/SKILL.md) | Find and update Wiki.js pages, checking the page's real editor/format before writing; verify automation exists before writing manual steps |
| [permission-promote](./permission-promote/SKILL.md) | Review repeated permission interruptions and promote only narrow, sandbox-consistent grants |

<!-- Add rows as skills are created. Example:

| [describe-feature](./describe-feature/SKILL.md) | Define, scope, and document a feature before implementation |
| [implement-feature](./implement-feature/SKILL.md) | Implement a feature using project architecture rules |
-->

## Rules

- One folder per skill: `.agents/skills/kebab-name/SKILL.md` - no numeric prefix, this is a
  flat catalog, not a sequence.
- Optional subdirectories: `references/`, `scripts/`, `assets/` for supporting material.
- The `name` field in SKILL.md frontmatter must match the folder name.
- Update this catalog when adding or removing a skill.
- See [_SKILLS_TEMPLATE.md](_SKILLS_TEMPLATE.md) for the full pattern.
