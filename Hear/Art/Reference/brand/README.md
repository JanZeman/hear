# Brand assets

- `v3.1/` — **canonical logo source, supersedes v2.2, v3.0 and every earlier logo asset/handoff.**
  SVG masters, transparent PNG exports (with-claim / no-claim / mark-only, each on-light +
  on-dark, multiple resolutions), the approved design rules
  (`v3.1/docs/HEAR-LOGO-DESIGN-RULES.md`), and the QA preview board rendered from the actual
  exported PNGs (`v3.1/previews/hear-logo-system-board-v3.1.png`) - use that board to visually
  QA any future integration change. The running app's `Assets/HearApp/Resources/Brand/` PNGs are
  a straight copy of this package's exports (currently the -1024/-512 sizes) - do not redraw or
  reinterpret geometry/spacing/proportions; a resolution swap from this same package is fine,
  anything else needs a new handoff.
- `v2.2/` — superseded by v3.1; never shipped (integrated once, then reverted before v3.1
  arrived). Kept for history only.
- `hear-mark.svg`, `hear-lockup-reference.svg` — superseded working drafts from before v2.2;
  kept for history, not for new work.
