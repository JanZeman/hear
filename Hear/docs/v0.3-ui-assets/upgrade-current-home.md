# Upgrade the current desktop home screen

The current implementation in `references/current-implementation.png` is structurally functional but visually too close to an admin dashboard and too far from HEAR's intended immersive shell.

## Required corrections

1. **Use the real HEAR identity.** Replace the improvised sidebar lockup with `assets/brand/hear-logo-light-1024.png` or reconstruct from the supplied mark + Inter according to the logo spec. Do not place the 4 shapes beside a bold HEAR wordmark; the primary lockup has the 4 elements aligned over H/E/A/R.
2. **World first, chrome second.** The selected world must dominate the visual field. Wide layouts should use the supplied `ambient-bg-16x9.jpg` as an atmospheric extension behind/around the hero region rather than leaving most of the main canvas flat white. Keep a restrained Pearl shell where needed for legibility.
3. **Use the Companion as a real separate sprite.** Place `companion-neutral-1024.png` in the active-world hero. It may sit on a dock/rock/ledge, with world-specific position and scale. It is not part of the logo and should not be baked into every thumbnail.
4. **Use the clean world assets.** Use `world-16x9.jpg` for wide hero regions and the supplied ratio-specific images for cards. Do not use the original annotated concept boards with titles/scores/storyboard baked in.
5. **Reduce dashboard feeling.** Avoid large blank white regions, heavy selected-blue outlines, oversized utility chrome, and placeholder gray-dot icons. Use world atmosphere, soft radii, subtle borders, and restrained aurora accents.
6. **Typography.** Use Inter semantic styles from the visual bible. World title is the dominant text. Navigation is quieter. Do not use heavy/bold typography everywhere.
7. **Navigation.** For the current prototype expose only destinations that really exist. `Worlds`, `Results`, `Settings` are enough; do not invent Profile/user data. If a Home destination remains, it should mean the world selector and should not duplicate a separate Worlds page unnecessarily.
8. **No invented personalization.** Remove `Good morning, Jan` and avatar unless an actual profile feature is intentionally implemented. HEAR should not ask for or pretend to have personal data.
9. **Selected card.** Prefer a subtle aurora outline/glow and scale/elevation change. The current strong cyan border is too utilitarian.
10. **Responsive behavior.** Wide=sidebar, medium=icon rail, compact portrait=bottom nav. The active world safe composition stays valid in a central 1:1 region. Never stretch artwork non-uniformly.

## Wide desktop composition target

- Sidebar: quiet, approximately 200–240 logical px.
- Main content padding: 24–32.
- Hero: roughly 45–55% of usable content height above the fold, radius 24–32.
- Hero image: edge-to-edge world art with a local dark scrim only behind copy.
- Companion: roughly 15–25% of hero height depending on scene, placed as a physical inhabitant rather than floating UI.
- World cards: image-dominant; minimal copy; 16:9 or 4:3 depending available width.
- Empty space is allowed, but it should feel intentional and gallery-like, not like an unfinished dashboard.

See `references/target-macos-wide.png` and `target-windows-wide.png` for hierarchy/mood, not pixel-perfect tracing.
