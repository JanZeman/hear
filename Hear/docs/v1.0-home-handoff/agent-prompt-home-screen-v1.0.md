Use `hear-home-screen-handoff-v1.0.zip` as the single canonical handoff package for implementing the HEAR Home / World Carousel screen.

This package consolidates the locked logo, locked Companion sprite pack, world art, icons, metadata, and references required for Home.

AUTHORITY ORDER:
1. `references/00-GOLDEN-home-concept-board.png`
2. runtime assets in `assets/`
3. metadata in `metadata/`
4. docs in `docs/`
5. derived targets in `references/derived/`

If anything conflicts, the GOLDEN board wins visually, but implementation must use the provided independent runtime assets rather than crop from the board.

MANDATORY RULES:
- Do not redraw or reinterpret the HEAR logo.
- Use the supplied locked logo assets only.
- The HEAR logo is exactly 4 elements aligned one-to-one over H E A R:
  dot over H, small ellipse over E, medium ellipse over A, large ellipse over R.
- `Sound opens worlds.` is separate live UI text, not part of the logo image.
- Do not redraw or approximate the Companion.
- Do not add feet, legs, toes or a visible body base to the Companion.
- Use the Companion as a separate runtime sprite.
- Use the supplied separate shadow asset when grounding the Companion.
- Use a sharp active-world background, not a generic blurred fill.
- Home must be a real carousel: one large selected world card, neighboring worlds peeking from the sides, dots, one Play button.
- Do not use `FEATURED WORLD`.
- Do not use a separate row of thumbnails under the hero.
- Do not add profile/avatar/greeting filler.
- Bottom navigation in compact layouts must remain overlay-style and subtle, not a plain full-width black or white slab.
- iOS and Android share the same HEAR visual design; only safe areas/insets differ.
- Never non-uniformly stretch artwork.

IMPLEMENTATION STEPS:
1. Read `README.md`.
2. Open `references/00-GOLDEN-home-concept-board.png`.
3. Read `docs/00-home-design-freeze.md`, `docs/01-asset-map-consolidated.md`, and `docs/02-developer-checklist.md`.
4. Read `metadata/home-layout.json` and `metadata/companion.json`.
5. Compare the current implementation with the GOLDEN board and write a short delta list.
6. Correct the Home screen toward the GOLDEN board using the supplied assets.

HOME COMPOSITION TARGET:
- active world atmosphere fills the viewport
- HEAR logo and claim near the top
- central selected River Journey card
- Tide Troubles peeking on one side
- The Paper Garden peeking on the other side
- dots below the card
- one Play pill below
- Companion near the lower-right action area, looking inward, grounded with a separate shadow
- subtle bottom navigation on compact layouts
- restrained sidebar only on wide desktop

ASSET USAGE:
- Logo: `assets/brand/`
- Companion: `assets/companion/`
- World art: `assets/worlds/`
- Icons: `assets/icons/`
- Layout guidance: `metadata/home-layout.json`
- Companion placement guidance: `metadata/companion.json`

DELIVERABLES:
When finished, report:
- what changed
- which supplied assets are used
- any temporary placeholders
- any deviation from the GOLDEN board and the exact technical reason
- screenshots for Android portrait, iOS portrait, Fold/square, tablet landscape, wide desktop, and narrow desktop

QUALITY GATE:
If the resulting Home still feels like a generic dashboard rather than an immersive world selector, the task is not complete.
