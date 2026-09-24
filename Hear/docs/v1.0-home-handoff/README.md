# HEAR Home Screen Handoff v1.0

This is the consolidated, authoritative handoff package for implementing the **HEAR Home / World Carousel** screen.

It combines the locked HEAR logo assets, the locked Companion production sprite pack, the current approved world assets, the Home layout metadata, the GOLDEN concept board, and a single implementation prompt for the coding agent.

## What this package is for
Use this package to implement or correct the HEAR Home screen in Unity.

This package is intentionally focused on **Home**. It is not the whole product handoff.

## Authority order
1. `references/00-GOLDEN-home-concept-board.png`
2. locked runtime assets in `assets/brand/`, `assets/companion/`, `assets/worlds/`, `assets/icons/`
3. `metadata/home-layout.json` and `metadata/companion.json`
4. docs in `docs/`
5. derived runtime targets in `references/derived/`

If anything conflicts, the **GOLDEN concept board wins visually**, but production implementation must use the provided independent assets rather than cropping from the board.

## Frozen design decisions
- HEAR logo = exactly **4 elements** aligned one-to-one above **H E A R**:
  - dot over H
  - small ellipse over E
  - medium ellipse over A
  - large ellipse over R
- The logo is locked. Do not redraw or reinterpret it.
- The dot is contrast-adaptive:
  - dark on light backgrounds
  - light on dark backgrounds
- `Sound opens worlds.` is **not part of the logo artwork**. Render it as live UI text.
- Companion has **no feet / no legs / no toes / no visible base**.
- Companion must stay a separate runtime sprite, not be baked into the background.
- Home background must be the **sharp active-world artwork**, not a generic blur fill.
- Home must be a **carousel** with one large selected card and neighboring cards peeking from the sides.
- No `FEATURED WORLD` label.
- No separate thumbnail row under the hero.
- Bottom navigation is an **overlay over the world**, not a solid full-width black or white slab.
- iOS and Android share the same HEAR visual design; only safe areas / system insets differ.

## Main folders
- `assets/brand/` — canonical logo files
- `assets/companion/` — canonical Companion sprites + separate shadows
- `assets/worlds/` — world artwork and Home backgrounds
- `assets/icons/` — navigation and utility SVGs
- `metadata/` — layout and placement JSON
- `references/` — GOLDEN board + secondary runtime target references
- `docs/` — implementation notes, checklists, asset maps
- `scripts/` — validation helper(s)

## Recommended first step for the developer agent
Read `agent-prompt-home-screen-v1.0.md` and then inspect the GOLDEN board before changing any code.
