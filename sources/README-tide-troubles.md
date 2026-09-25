# HEAR – Tide Troubles handoff v1.0

This package contains the first production handoff for the **Tide Troubles** world.

## Intent
Tide Troubles is a lively but readable **2D harbor reaction/listening world** for HEAR.
The player should feel that they have entered a sunny animated harbor where fish pop up,
seagulls fly across the scene, floating objects bob in the water, and successful actions
trigger satisfying splashes and glow feedback.

## Included assets

### Backgrounds
- `assets/backgrounds/sunset-harbor-lighthouse-view.png`
  - main scenic harbor background / sky / sea / cliffs / harbor buildings
- `assets/backgrounds/golden-hour-seaside-dock-frame.png`
  - foreground dock frame layer to sit in front of the play area

### Sprites
- `assets/sprites/fish/cute-fish-sprite-sheet-grid.png`
  - fish variants and fish-in-splash variants; use as target characters
- `assets/sprites/seagulls/seagull-flight-pose-sprite-sheet.png`
  - seagull poses for ambient motion
- `assets/sprites/floating-objects/sunset-harbor-buoy-sprite-sheet.png`
  - buoys, floating lighthouse buoy, barrels, crates, floats
- `assets/sprites/launcher/harbor-net-cannon-sprite-sheet.png`
  - net launcher / cannon and loose net states
- `assets/sprites/effects/vibrant-water-splash-sprite-sheet.png`
  - water splash / ripple / droplets
- `assets/sprites/effects/golden-fishing-effects-asset-sheet.png`
  - target rings, glow hits, water hit effects, celebration effects
- `assets/sprites/dog/playful-harbor-terrier-sprite-sheet.png`
  - playful harbor dog reactions
- `assets/sprites/companion/cute-translucent-ghost-mascot-sprite-sheet.png`
  - HEAR companion reactions

### References
- `references/scene-fish-hunt.png`
- `references/scene-listening-quest.png`
- `references/scene-catch-celebration.png`
- `references/scene-tap-target-at-sunset.png`

These references communicate mood, composition and gameplay feeling, not strict UI layout.

## Recommended scene structure in Unity
1. **Far background**: scenic harbor background.
2. **Mid gameplay band**: fish, floating objects, target rings, splashes.
3. **Ambient layer**: seagulls, small moving boats if desired, floating props.
4. **Foreground**: dock frame layer and optional dog / companion anchors.
5. **UI layer**: score, progress, cue indicators and interactive prompts.

## Recommended gameplay loop
- A sound cue or prompt occurs.
- One or more fish become valid targets.
- The player reacts by tapping or aiming the net launcher.
- Correct result: splash + glow + celebratory dog/companion reaction.
- Miss / wrong response: softer splash or reduced feedback.

## Important implementation notes
- Treat all provided sheets as **source concept assets** to be sliced by engineering.
- Preserve overall warm sunset palette and soft cartoon rendering.
- Keep the scene clearly readable; do not overload the play area.
- Prioritize foreground / gameplay readability over full-scene detail.
- Use subtle parallax rather than camera motion.

See `agent-prompt-tide-troubles-v1.0.md` for a ready-to-send developer prompt.
