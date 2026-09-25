# Tide Troubles v1.0 - implementation notes

Implements `agent-prompt-tide-troubles-v1.0.md` against the assets in this folder's sibling
`README.md` (mirrored from `hear-tide-troubles-handoff-v1.0`). Replaces the earlier greybox
colored-quad placeholder scene (`Assets/HearApp/Worlds/TideTroubles/TideTroublesPresentation.cs`)
with the real concept art, sliced at runtime.

## Slicing decisions

There is no Unity Editor session available in this environment (no Sprite Editor pass), so sheets
are sliced by hand-authored pixel rects in `TideTroublesArt.cs` instead of Editor-side
grid/automatic slicing, cut at runtime via `Sprite.Create` from plain imported `Texture2D`
Resources (same import settings as the rest of `Assets/HearApp/Resources`, e.g. `WorldArt.cs`'s
Companion textures) rather than Editor-authored multi-sprite `.meta` files.

- **Fish** (`fish-sheet.png`, 1448x1086): clean 3x3 grid. Rows = species (blue/gold/puffer),
  columns = pose (idle / jump-splash / caught-squint). Cell 482x362px.
- **Seagulls** (`seagull-sheet.png`) / **Dog** (`dog-sheet.png`): clean 4x2 grids, 8 pose frames
  each, cell 362x543px.
- **Companion** (`companion-sheet.png`): near-uniform 4x3 grid, 12 pose frames, cell 362x362px.
- **Buoys/crates** (`buoy-sheet.png`): irregular 5-row kit (flags/lighthouses/barrels/
  crates/misc floats). Only 4 curated picks are used (one per row-type) rather than every
  variant - enough for ambient variety without over-scoping the first pass.
- **Net cannon** (`launcher-sheet.png`): irregular layout. 3 curated picks used: idle-left,
  idle-right (both already aim inward toward center on their own side, so no mirroring is
  needed), and one loose-net pose for the flying net. The sheet's "firing" pose (cannon + a long
  already-flying net baked into one image) is intentionally unused - it would visually conflict
  with the separately animated flying net object.
- **Water splash** (`splash-sheet.png`): dense multi-row effects kit. 3 splash + 2 ripple rects
  curated from the cleanest, most separated elements rather than gridding the whole sheet.
- **Golden fishing effects** (`effects-sheet.png`): 2 curated picks (gold target ring,
  celebration sparkle burst) out of the sheet's rings/crowns/bursts.
- **Backgrounds** (`harbor-background.png`, `dock-frame.png`, both 941x1672 portrait): imported
  whole, single sprite each, no slicing needed.

Rects are generous rather than pixel-exact - every sheet has ample transparent padding around
each element, and the README explicitly frames these as "source concept assets to be sliced by
engineering," so approximate-but-correct slicing matches the intended quality bar for this pass.

## Scene hierarchy (all procedural, built in `TideTroublesPresentation.Awake`)

No `.prefab` assets exist anywhere in this project (every world builds its scene from code); this
follows that existing convention rather than introducing prefabs as a new pattern. The
agent-prompt's "prefabs for FishTarget/SeagullAmbient/FloatingProp/SplashEffect/DogReaction/
CompanionReaction" are implemented as the code-level builder methods below instead.

Layering uses `SpriteRenderer.sortingOrder` exclusively (not Z/camera transparency-sort), so it
is correct regardless of project graphics settings:

```text
HarborBackground        (-100, BuildBackdrop)
FloatingProp_0..4       (-10,  SpawnFloatingProps - buoys/crates, bob via Update)
Seagull_0..2            (-5,   SpawnSeagulls - horizontal flight loop, wraps at screen edges)
Fish_0..5               (0,    SpawnFishTargets - the only capturable targets)
LauncherLeft/Right, Net (5/8,  BuildCaptureGagRig)
Ripple / Splash / TargetRingGold (8/9/10, transient, CaptureGag beat 4)
CelebrationBurst        (14,   transient, ReactionPop)
DogReaction, CompanionReaction (15, BuildReactionCast)
DockFrame               (20,   BuildBackdrop - frontmost, transparent-center wooden overlay)
```

Background and dock frame use a `cover` fit (uniform scale to `max(requiredW/spriteW,
requiredH/spriteH)`, never non-uniform stretch) against the Core Safe Square's current
`orthographicSize`/aspect, matching `docs/v0.3-ui-assets/asset-usage.md`'s cover-crop rule for
world art.

## Gameplay behavior preserved from the greybox pass

- `PickTarget` / `GetLauncher` lateralization (Left/Right/Combined) is unchanged, now operating
  over `FishInstance` instead of a plain `Transform` list.
- The five-beat `CaptureGag` choreography (anticipation -> launch -> hit-stop -> reaction ->
  release) is unchanged; only the visuals it drives changed (real sprites instead of colored
  quads, plus a splash/ripple/glow pop and a dog+companion happy-pose bounce during beat 4).
- Only fish are valid capture targets, per the agent-prompt's asset-role split (seagulls/floating
  props are explicitly "ambient"/"bobbing props", not targets). Their motion runs on independent
  timers (`Update`, `FishAmbientLoop`) uncorrelated with tone onset, per docs/07's ear-aware rule.

## Known limitations / not done in this pass

- Slicing rects were authored from visual inspection of each sheet (no PIL/numpy/Editor available
  in this environment to do connected-component auto-slicing), so they are approximate, not
  pixel-perfect. A human/Editor pass with the Sprite Editor could tighten these.
- This has not been build- or Play-mode-verified in this session (no Unity Editor run here - see
  the main `README.md`'s own "Known compromises" section for why that verification path is
  already unreliable/unavailable in this environment). The code was written and reviewed
  carefully against the existing `WorldPresentationBase` contract and the prior working
  implementation it replaces, but a human/CI run through the Unity Editor is still needed before
  this ships.
- The buoy and splash/effects sheets are only partially used (curated picks, not every variant);
  more variety could be added later without any architecture change.
- Session-progress-driven harbor evolution (time of day, background boats) remains an unfilled
  hook (`SetSessionProgress`), unchanged from the prior pass.
