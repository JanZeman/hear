# River of Echoes — agent handoff (session paused: low tokens)

## Task (verbatim from user, Czech)
"Tu 'indianskou' scenku jsme prejmenovali na 'River of Echos'. Ve srovnani s 'Tide Troubles'
bychom chteli dosahnout mnohem lepsiho 3D efektu."

Then: implement the HEAR world "River of Echoes" using the handoff package as source of truth.
Read the three briefs in order, preserve the one-directional journey-home continuity and
portrait-safe framing, implement as one scoped stylized real-time 3D Unity scene with guided
canoe movement, left/right paddle success feedback, progressive village reveal, and the chief
welcoming the player at the end.

## Source material (already read in full this session)
Handoff zip: `/Users/jan/Dev/HR/sources/Worlds/RiverOfEchos/river_of_echoes_final_handoff_package.zip`
(note: this path is in the **other worktree** `HR`, not `HR1` — read directly via absolute path,
that's fine, worktrees share the same disk).

Extracted copy for reference: `Hear/docs/v1.0-river-of-echoes-handoff/` (created this session,
currently only has this handoff note — the actual docs/image are still sitting in the scratchpad
at `/private/tmp/claude-501/-Users-jan-Dev-HR1/8cbc9c39-4218-4116-9e46-0d01c8cdf8a7/scratchpad/roe/`
which will NOT survive past this session — re-extract from the zip path above if that scratchpad
is gone).

Contents (docs read, all digested — summarized below so you don't need to re-read unless you
want exact wording):
- `00_README_AND_MASTER_HANDOFF.md` — read order, locked creative rules.
- `01_river_of_echoes_continuity_brief.md` — the 10-step one-directional journey-home storyboard
  continuity rules.
- `02_river_of_echoes_paddle_animation_brief.md` — paddle-feedback micro-animation spec
  (Idle Drift / Paddle Left / Paddle Right / optional Near-Dock Slow Arrival).
- `03_river_of_echoes_unity_implementation_brief.md` — the actual Unity implementation plan,
  suggested class architecture, movement model, milestones, acceptance criteria.
- `references/river_of_echoes_storyboard_reference_v1.png` — 10-panel storyboard, viewed this
  session. Shows: canoeist seen from behind in a hide/bark canoe, paddling on a wide reflective
  river toward a right-bank village (tipis, fire, smoke, banners, dock posts), sunset mountain
  backdrop. Panels 1-4 = distant approach, 5-6 = village readable + second boost, 7-8 = villagers
  notice/wave, 9-10 = arrival at dock where the chief (feathered headdress) personally greets him.

**IMPORTANT: there are NO 3D model/art assets in this handoff package** — only the 4 markdown
docs and one reference PNG. Unlike Tide Troubles (which shipped full sprite sheets), everything
for River of Echoes must be built procedurally in code (canoe, cliffs, trees, tipis, villagers,
water) using primitives/simple meshes/particles/lighting — consistent with this project's
existing "no prefabs, everything built at runtime in code" convention, just now in 3D instead of
UI Toolkit 2D.

## Key locked rules (do not violate)
- World name: **River of Echoes** (canonical, exact).
- Style: stylized real-time 3D, NOT flat 2D, NOT photoreal, NOT oversaturated "AI wallpaper".
- One continuous Unity 3D scene, NOT ten separate scenes/levels, NOT a prerendered video.
- Canoe always drifts forward at low speed; never moves backward in space or story.
- Village is on the **right bank** always; canoe trajectory gradually converges toward it.
- Success (per HEAR's existing hearing-trial outcome) → paddle stroke (left or right, mapped
  from the trial's ear channel; alternate if channel is Combined/no side info) → splash on that
  side → short forward speed boost → smooth blend back to baseline drift.
- Progressive village reveal driven by a single normalized `JourneyProgress` (0..1), not
  hardcoded per-frame scenes: distant silhouette → tipis/smoke/dock readable → villagers readable
  → villagers notice/wave → chief visible and welcomes at the end.
- Chief is the final welcoming figure. **No cute animal/dog side character** (explicit exclusion).
- Camera: third-person, behind and low-medium over the water, portrait-safe crop (canoe + village
  both must stay readable in a mobile portrait aspect, not just landscape).
- Sunset lighting subtly progresses (warm orange/gold/pink/restrained purple) but never jumps to
  night within the ~30s session.
- Mobile performance: avoid heavy physics/GI/volumetrics/high poly counts; keep it technically
  modest — richness from composition/lighting/atmosphere, not brute force.
- Suggested class breakdown (names are suggestions, not mandatory): `RiverWorldController`,
  `CanoePathController` (spline/waypoint drift + boost, no boat physics), `PaddleFeedbackController`,
  `RiverCameraController`, `VillageProgressController`, simple villager idle/notice/wave state
  logic.
- Suggested milestone order (recommended to follow, mirrors how Tide Troubles was built
  incrementally with on-device feedback rounds): 1) greybox (path/camera/placeholder canoe/
  portrait framing) 2) core paddle feedback (idle/left/right/boost/splash) 3) environment (river
  material, cliffs, trees, village placement, dock, sunset light) 4) village progression (reveal
  logic, smoke/fire, villager idle/notice/wave, chief) 5) polish (animation feel, camera smoothing,
  VFX/color tuning, portrait verification, perf pass).

## Open question for next session — CHECK THIS FIRST
Found during exploration, **not yet resolved**: `Assets/HearApp/Core/Worlds/WorldRegistry.cs`
already lists **three** worlds, including one called `"river-journey"` / "River Journey" with
scene name `RiverJourneyWorld`, and there is already a folder `Assets/HearApp/Worlds/RiverJourney/`
(plus `Assets/HearApp/Worlds/PaperGarden/`) in this HR1 worktree — presumably built by the other
agent in the main `HR` worktree and merged into `_hr1` since this session's last summary (this
session hadn't seen either of those worlds before now).

**Before writing any new code**, read `Assets/HearApp/Worlds/RiverJourney/*.cs` in full and
figure out:
1. Is "River Journey" the same concept the user just renamed to "River of Echoes" (i.e. should
   this be a rename + rebuild of the existing RiverJourney world in place), or is it an unrelated
   world that happens to share a similar name?
2. If it's the same concept: update `WorldRegistry.cs`'s id/DisplayName/tagline/SceneName for that
   entry to "River of Echoes" (confirm exact display copy with user if unsure), and replace/extend
   the RiverJourney implementation rather than creating a duplicate fourth world.
3. If unrelated: create a new world following the existing pattern (new WorldEntry, new scene,
   new folder `Assets/HearApp/Worlds/RiverOfEchoes/`), same as Tide Troubles was added.

This determination was NOT made before the session was paused — do it before any implementation.

## Also not yet read (needed before implementing)
- `Assets/HearApp/Core/Worlds/IWorldPresentation.cs` and `WorldPresentationBase.cs` — the
  contract every world must implement (`PresentOutcome`, `SetSessionProgress`, `ListeningSafe`
  event, etc. — inferred from TideTroublesPresentation usage patterns seen earlier this session,
  but not re-confirmed against the actual interface this session).
- `Assets/HearApp/Core/Worlds/WorldContext.cs`.
- Whether this project's URP setup / build settings already support a real 3D scene (lighting,
  post-processing, water shader availability) or if that needs setting up from scratch — TideTroubles
  and (presumably) RiverJourney/PaperGarden are 2D UI Toolkit scenes; if RiverJourney already
  attempted a 3D approach, its code is the best starting reference for what's already proven to
  work in this headless/no-Editor-GUI pipeline (e.g. can prefabs/meshes only be built via code,
  same as 2D sprites were).

## Environment / workflow reminders (carried over from earlier in this session, still valid)
- Working directory: `/Users/jan/Dev/HR1/Hear` (git worktree `_hr1`, branch `_hr1`). Do not `cd`
  into other worktrees; read cross-worktree files via absolute path only when needed (as done here
  for the zip).
- All Unity/Xcode/devicectl commands require `dangerouslyDisableSandbox: true` (sandboxed builds
  fail with licensing/readonly-database errors).
- Build pipeline that works end-to-end (used successfully multiple times this session):
  ```
  cd /Users/jan/Dev/HR1/Hear
  unity build --target iOS --execute-method Hear.Editor.HearDevelopmentBuild.BuildIos --output-path Builds/iOS
  cd Builds/iOS
  xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Debug \
    -destination "generic/platform=iOS" -allowProvisioningUpdates CODE_SIGN_STYLE=Automatic \
    DEVELOPMENT_TEAM=9VBQGD32YX build
  xcrun devicectl device install app --device 00008101-001251CA1E31003A \
    /Users/jan/Library/Developer/Xcode/DerivedData/Unity-iPhone-fgvooyjpkuebzyfsywgveqnuosiv/Build/Products/Debug-iphoneos/Hear.app
  xcrun devicectl device process launch --device 00008101-001251CA1E31003A com.janzeman.hear
  ```
  (Device name: "Jan GN iPhone Dev", UDID `00008101-001251CA1E31003A`. DerivedData path may change
  if Xcode regenerates it — re-run `xcodebuild` and read the actual `.app` output path from its log
  if the install step 404s.)
- This project has **no `.prefab` assets anywhere** — everything is built procedurally in C# at
  runtime (`Awake()`/scene-construction methods), matching `TideTroublesPresentation.cs`'s pattern.
  Apply the same discipline to River of Echoes: no Editor-authored prefabs, no Sprite/Animation
  Editor sessions (none available in this headless CLI environment) — build meshes, materials,
  particle systems, and lights entirely from code.
- User (Jan Zeman) iterates via real on-device testing on his physical iPhone, giving feedback in
  Czech in short rounds; expect the same build→deploy→feedback loop used for Tide Troubles.
- Per `CLAUDE.md`/`AGENTS.md` in this repo: session-start banner and `agent-base-guard.sh` are
  mandatory at the start of a session; `AB-GIT-002` requires explicit user confirmation ("kk"/"ok")
  before running git mutation commands.
- Uncommitted work exists in this worktree from earlier in the session (LongSession removal, fish/
  seagull/dog sprite re-cropping, points-cap-at-25, and the net-unfurl animation for Tide Troubles)
  — none of that has been committed yet. Check `git status` at the start of the next session before
  doing anything else; do not discard it.

## Immediate next steps for the resuming session
1. Re-run `git status` / `git diff --stat` to reconfirm the uncommitted Tide Troubles changes are
   still intact.
2. Read `Assets/HearApp/Worlds/RiverJourney/*.cs`, `IWorldPresentation.cs`, `WorldPresentationBase.cs`,
   `WorldContext.cs` to resolve the RiverJourney-vs-River-of-Echoes question above.
3. Re-extract the handoff zip if the scratchpad copy is gone (path given above) — or just rely on
   the summary in this document, which captures all locked rules and acceptance criteria in full.
4. Propose/confirm the milestone plan with the user before diving into full implementation, given
   the scale of a real-time 3D scene versus the UI Toolkit 2D worlds built so far — this is a
   meaningfully larger and more novel technical undertaking for this codebase.
5. Start at Milestone 1 (greybox) per the implementation brief, and get one on-device build in
   front of the user early rather than building everything before the first test.
