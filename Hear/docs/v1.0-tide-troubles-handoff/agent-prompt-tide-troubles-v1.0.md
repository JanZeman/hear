Use `hear-tide-troubles-handoff-v1.0` as the canonical implementation source for the Tide Troubles world.

GOAL
Implement the first playable version of the HEAR world `Tide Troubles` in Unity as a 2D scene.
It should feel like a lively sunset harbor reaction/listening game, consistent with the included references.

READ FIRST
- README.md
- all images under `references/`

ASSET ROLES
Backgrounds:
- `assets/backgrounds/sunset-harbor-lighthouse-view.png` = main world background
- `assets/backgrounds/golden-hour-seaside-dock-frame.png` = foreground dock overlay / frame

Interactive / ambient sprite sheets:
- `assets/sprites/fish/cute-fish-sprite-sheet-grid.png` = fish targets
- `assets/sprites/seagulls/seagull-flight-pose-sprite-sheet.png` = ambient seagulls
- `assets/sprites/floating-objects/sunset-harbor-buoy-sprite-sheet.png` = bobbing props
- `assets/sprites/launcher/harbor-net-cannon-sprite-sheet.png` = optional launcher / aiming object
- `assets/sprites/effects/vibrant-water-splash-sprite-sheet.png` = splash / ripple effects
- `assets/sprites/effects/golden-fishing-effects-asset-sheet.png` = target feedback / success glow / special feedback
- `assets/sprites/dog/playful-harbor-terrier-sprite-sheet.png` = dog reactions
- `assets/sprites/companion/cute-translucent-ghost-mascot-sprite-sheet.png` = HEAR companion reactions

IMPLEMENTATION TARGET
Build a playable prototype with these behaviors:
1. Entering the world shows the harbor background and immediate sense of place.
2. Fish appear as the main targets in the gameplay band.
3. Fish can jump or pop up from water positions.
4. A correct interaction triggers splash + glow + positive reaction.
5. Ambient seagulls and floating props make the scene feel alive.
6. The dock frame gives the scene a foreground edge and depth.

SCENE LAYOUT
- Use the harbor background as the static far layer.
- Use the dock frame as a front-most decorative layer.
- Place targets in a central gameplay area with strong readability.
- Keep fish large enough to read on mobile.
- Seagulls should sit higher in the frame and never interfere with core gameplay.
- Floating props should support the world, not clutter the target zone.

ANIMATION / MOTION STRATEGY
Do not attempt full-frame animation.
Instead:
- slice the provided sheets into separate sprites;
- animate via transforms, simple loops, and small sprite changes;
- bob props gently;
- move seagulls horizontally;
- spawn fish from water positions;
- use splash / ring assets for impact and feedback.

TECHNICAL DIRECTION
- import all PNGs with transparency preserved;
- slice sheets manually or by grid where practical;
- create prefabs for FishTarget, SeagullAmbient, FloatingProp, SplashEffect, DogReaction, CompanionReaction;
- create one self-contained playable scene for Tide Troubles;
- keep code structured so that a shared HEAR gameplay framework can later drive other worlds too.

QUALITY BAR
Aim for a scene that already feels like a coherent HEAR world, not just a test sandbox.
The included reference images define the emotional target: warm, playful, polished, readable, inviting.

DO NOT
- replace the style with generic placeholder art;
- overcomplicate the first version with physics-heavy systems;
- turn it into a side-scroller;
- let decorative elements reduce target readability.

DELIVERABLES
- a playable Tide Troubles Unity scene
- sliced/imported art assets placed in a clear folder structure
- prefabs for the main reusable entities
- a short implementation note describing slicing decisions and scene hierarchy
