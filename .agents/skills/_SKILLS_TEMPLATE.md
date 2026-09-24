# Skills Template

Skills follow the [Agent Skills](https://agentskills.io/specification) open format.
Each skill is a folder containing a `SKILL.md` with YAML frontmatter and Markdown instructions.

## Creating a new skill

1. Create the folder: `.agents/skills/skill-name/`
2. Copy `_SKILL_TEMPLATE/SKILL.md` into it.
3. Set `name:` to match the folder name (e.g. `describe-feature`).
4. Write a specific `description:` - agents use it to decide when to activate the skill.
5. Fill in the Markdown body with instructions.
6. Add the skill to `_SKILLS_CATALOG.md`.

## Directory structure

```
.agents/skills/
  _SKILLS_CATALOG.md           # Index of all skills
  _SKILLS_TEMPLATE.md          # This file
  _SKILL_TEMPLATE/SKILL.md     # Skeleton to copy
  describe-feature/
    SKILL.md                   # Full instructions + frontmatter
    references/                # Optional: detailed reference docs
    scripts/                   # Optional: executable code
    assets/                    # Optional: templates, schemas
```

## Naming rules

- Folder name: `kebab-name` (lowercase, hyphens only, no numeric prefix - this is a flat
  catalog of named skills, not a sequence)
- The `name` field in frontmatter must match the folder name exactly
- Names are permanent - never renamed or reused for a different skill

## Progressive disclosure

Agents load skills in three stages:

1. **Discovery**: only `name` and `description` are read (all skills, at startup)
2. **Activation**: the full `SKILL.md` body is loaded when the task matches
3. **Resources**: files in `references/`, `scripts/`, `assets/` are loaded on demand

Keep `SKILL.md` under 500 lines. Move detailed reference material to `references/`.
