# Asset usage and cropping rules

## Source hierarchy

1. Use an exported asset in `assets/` if one exists.
2. Use `references/` only to understand composition/mood.
3. Do not extract UI controls, logos, or Companion sprites from reference boards yourself. Design has already exported the approved prototype assets.

## World art

Each world contains a clean master and precomputed ratio variants. Choose the nearest ratio and use a **cover** strategy: proportional scale + crop. Never non-uniformly stretch.

- wide desktop hero: `world-16x9.jpg`
- medium cards: `world-4x3.jpg` or `world-16x9.jpg`
- Fold/square: `world-1x1.jpg`
- narrow portrait preview: `world-9x16.jpg`
- shell atmosphere: `ambient-bg-16x9.jpg` behind a scrim/blur layer

For gameplay scenes these are placeholders/style references. Build the actual 2D/2.5D/3D world from separate scene assets later.

## Companion

Transparent PNG. Preserve aspect ratio. Do not colorize heavily. A subtle world ambient tint (5–15%) is allowed, plus a separate Unity shadow.

## Logo

Primary static geometry: dot + three growing ellipses aligned over H/E/A/R. Keep the mark quiet. Sound-wave opening belongs to motion, not the static logo.

## Core Safe Square

Gameplay-critical and home-card focal content must remain meaningful in the central 1:1 region. Extra width or height reveals atmosphere; it must not be required to understand the world.

## Companion render size

Use the 512 px export by default. In the current prototype shell, keep the Companion at roughly <=220 logical px height; the source concept art is not intended for billboard-scale rendering. Use the 256 px version for cards/small UI.
