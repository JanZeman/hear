# Asset map (consolidated)

## Brand
Authoritative source: `assets/brand/`
Use:
- `hear-logo-on-dark.svg` / `hear-logo-on-light.svg` for full logo
- `hear-mark-on-dark.svg` / `hear-mark-on-light.svg` for symbol-only use

## Companion
Authoritative source: `assets/companion/`
Use:
- `sprites/on-dark/...` for dark/cinematic world backgrounds
- `sprites/on-light/...` for light UI backgrounds
- `shadows/` for separate grounding shadows

Default portrait Home recommendation:
- pose: `neutral-left`
- theme: `on-dark`
- placement: lower-right, looking inward

## Worlds
Authoritative source: `assets/worlds/`
Use clean art only.
Do not crop UI elements from any board.

River Journey additionally includes dedicated Home backgrounds:
- portrait
- square
- landscape
- ultrawide

## Icons
Use the SVG icon set in `assets/icons/`.
Current package includes the core functional icons.
If later a glowing HEAR icon style is created, that will supersede these.

## Metadata
- `metadata/home-layout.json`
- `metadata/companion.json`

## References
- `references/00-GOLDEN-home-concept-board.png` = highest visual authority
- `references/derived/` = secondary implementation targets
- `references/companion/` = companion contact sheets for quick review only
