# Suggested coding-agent start prompt

Read this entire handoff package before modifying the project. Inspect the existing Unity repository and current prototypes first, then propose the smallest architecture change that cleanly separates the hearing/session engine, global HEAR shell, and world presentation. Implement the first milestone described in `docs/11-implementation-plan.md` and satisfy `docs/12-acceptance-criteria.md`. Use the supplied images only as visual references, not as production assets. Preserve existing working code where sensible, keep the implementation responsive across mobile, foldable, macOS and Windows resizable windows, and stop only for a genuine product/authority blocker listed in `docs/13-open-questions.md` or an equivalent newly discovered blocker.

## v0.2 visual implementation note

Before implementing production-facing shell UI, read `docs/14-visual-bible.md` and use `assets/brand/hear-visual-tokens.json` as provisional centralized tokens. The visual bible expands the earlier short brand note and takes precedence on visual implementation details where they differ.
