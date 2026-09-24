# Hear

**Hear** is a standalone hearing game for players of any age. The full product and design
decisions live in `docs/`; this README describes the resulting code architecture.

The current milestone is a technical vertical slice proving one shared hearing/session
architecture can drive three radically different presentation worlds. It is not yet a validated
audiometric protocol.

The Unity project uses Unity 6 (`6000.6.2f1`) and the Universal Render Pipeline (URP). Its
application identifier is `com.janzeman.hear`.

## Project layout

```text
Assets/HearApp/
  Core/HearingEngine/   - measurement truth, owns nothing visual
  Core/Shell/            - navigation/state machine + UI Toolkit chrome
  Core/Worlds/           - the IWorldPresentation contract + shared helpers
  Worlds/TideTroubles/   - 2D world
  Worlds/PaperGarden/    - 2.5D world
  Worlds/RiverJourney/   - 3D world
  Dev/                   - development-only tooling (compiled out of release builds)
  Editor/                - scene/build scripts
  Scenes/                - Bootstrap.unity (persistent) + one additive scene per world
```

## What's shared across all three worlds

- **`TrialEngine`** (`Core/HearingEngine/TrialEngine.cs`) is the single source of truth: it
  schedules tone/catch trials, synthesizes and pans the stimulus (`TonePlayer`), captures
  taps, classifies the outcome, and updates `SessionResult`/progress. Its `ProcessTrial`
  method is called by both the real scheduling loop and the development mock driver, so
  "same sequence in -> same stored result out" holds by construction rather than convention.
- **`IWorldPresentation`** (`Core/Worlds/IWorldPresentation.cs`) is the only channel a world
  has into the engine. Worlds receive `OutcomePresentationContext` (classified outcome, ear
  channel, session progress) — never a stimulus-onset callback.
- **`CoreSafeSquareFit`** (`Core/Worlds/CoreSafeSquareFit.cs`) implements the Core Safe Square
  responsive rule for both orthographic (2D/2.5D) and perspective (3D) cameras by holding the
  matching dimension fixed and letting the other reveal more — one component, reused by all
  three world cameras, with no non-uniform stretching anywhere.
- **`GameFlowController`** (`Core/Shell/GameFlowController.cs`) owns the whole navigation state
  machine (splash → world selector → headphone/speaker choice → micro-instruction → play →
  results) and additively loads/unloads whichever world scene is selected.
- **`ShellUIController`** (`Core/Shell/UI/`) is the UI Toolkit shell: brand mark, world
  selector carousel (stable order, cold-start-randomized active index), headphone/speaker
  choice, minimal-chrome gameplay HUD, results, settings stub, and the
  Wide-sidebar/Medium-icon-rail/Compact-bottom-nav responsive navigation.

## What deliberately remains world-specific

Each world under `Worlds/` owns its own scene, camera, ambient visuals, and Success Event —
none of this logic lives in the engine or shell:

- **Tide Troubles** (2D): ambient creatures on independent timers; a `CorrectDetection`
  triggers an autonomous capture gag, never requiring the tap to land on anything.
- **The Paper Garden** (2.5D): flat placeholder layers at distinct Z depths; a
  `CorrectDetection` advances one step through an ordered list of stage actions.
- **River Journey** (3D): a primitive canoe drifts along a hand-rolled waypoint path purely
  from `SetSessionProgress`; a `CorrectDetection` only adds a cosmetic paddle bump plus a
  short, decaying forward-distance impulse on top.

All three deliberately implement the same `PresentOutcome`/`SetSessionProgress` contract with
no special-casing in the shared code — if a future world needs an engine/shell exception, that
should trigger revisiting the abstraction rather than adding another `if`.

## Responsive behavior

- Shell navigation swaps Sidebar → Icon rail → Bottom nav at width breakpoints (900px / 600px
  placeholders — exact values are an open question per `docs/13`).
- World cameras use `CoreSafeSquareFit` so landscape reveals more horizontally and portrait
  reveals more vertically, without ever stretching art non-uniformly.
- The macOS build defaults to a resizable **windowed** 1280×800 window (not fullscreen).

## Development tooling

With `DevOverlay` (press `` ` `` to toggle, Editor/Development builds only):

- Inject `CorrectDetection` / `Miss` / `FalsePositive` / `CorrectRejection` ×
  `Left`/`Right`/`Combined` directly into a running session, bypassing real audio timing.
- Quick-start any world without replaying the full shell flow.
- Run the **Integration Proof**: loads each world scene in isolation, runs the identical
  `MockSequenceDriver` sequence through a fresh `TrialEngine`, and logs whether the resulting
  `SessionResult` is identical across all three worlds.

The same proof is available via `HearApp/Editor/HearAppIntegrationProofCli.cs`
(`Hear > Run Integration Proof`), though see "Known limitations" below.

## Building

- `Hear > Build All Scenes` — (re)builds Bootstrap + the 3 world scenes and registers them
  in Build Settings.
- `Hear > Build Development > Android` — builds `Builds/Android/Hear.apk` as ARM64/IL2CPP.
- `Hear > Build Development > iOS` — generates the Xcode project in `Builds/iOS/`.
- `Hear > Build Development > macOS` — builds `Builds/macOS/Hear.app`.
- Android and macOS also provide an `and Run` command.

## What can be tested now

- Full shell flow (splash → selector → headphone choice → micro-instruction → play → results)
  in the Editor or the macOS build.
- All four `TrialOutcome` values and all three `EarChannel` values, via `DevOverlay`.
- Responsive behavior by resizing the Editor Game view or the macOS window.
- The Integration Proof, via the `DevOverlay` button (interactive) inside a running session.

## v0.3 UI asset upgrade

The Home/World Selector screen was rebuilt using the real assets shipped in
`hear-ui-assets-v0.3.zip` (archived in `docs/v0.3-ui-assets/` and
`Art/Reference/v0.3/`), replacing the earlier abstract-shape placeholder shell:

- **Brand**: `BrandMarkView` now displays the actual supplied `hear-logo-light-1024.png` (full
  lockup, splash screen) and `hear-mark-light-1024.png` (symbol-only, quiet nav header) instead of
  procedurally-drawn ellipses.
- **Companion**: `companion-neutral-512.png` is placed as a real sprite inside the active world's
  hero, at a per-world normalized anchor/scale (`WorldArt.GetCompanionPlacement`) rather than a
  fixed UI-coordinate mascot.
- **World art**: the World Selector is now a dominant hero (using `world-16x9`/`world-1x1`/
  `world-9x16` depending on the hero's live aspect ratio) with a scrim, title, tagline and Play
  button, plus a row of `world-4x3` thumbnails below - not an equal-weight card grid.
- **Ambient background**: `ambient-bg-16x9.jpg` per world crossfades behind the content
  (Motion-Scene ~550ms) when the selected world changes, replacing the earlier flat color wash.
- **Navigation**: `ResponsiveNavBar` uses the real icon set (rasterized from the supplied SVGs via
  `rsvg-convert`, tinted through `unityBackgroundImageTintColor`) and is deliberately slim
  (168px wide / 60px rail) so it stays subordinate to the world, per the v0.3 brief. No
  Profile/avatar/greeting was added - none exists in the current nav, matching the explicit
  instruction not to invent personalization.
- **Headphone screen**: now shows the real headphones icon above the (unchanged) copy.

### Known compromises in this pass

- Companion placement is an approximate manual per-world anchor (`WorldArt.cs`), not a
  pixel-accurate physical integration - the supplied world art has no reserved landing surface for
  it, unlike the original concept board composite.
- `IStyle.unityBackgroundScaleMode` is used for the cover-crop behavior; Unity 6 flags it as
  obsolete in favor of newer `background-*` USS properties, but those are not yet used elsewhere
  in the project and the deprecated API is still fully functional.
- Verification here was build + Player.log inspection + AppleScript-driven window resizes across
  wide/medium/narrow (no crashes/exceptions at any size). Screenshots could not be captured in
  this environment (`screencapture` lacks Screen Recording permission for this terminal) - visual
  confirmation still needs a human looking at the running app.
- Secondary screens (Micro-instruction, Results, Settings, Playing HUD) were intentionally left
  as-is per the requested priority order (home screen first).
- Windows behavior could not be tested (macOS-only environment); the UI Toolkit code is
  platform-agnostic, but this is unverified on that platform.

## Known limitations / placeholders

- World-selection art and shell assets are supplied prototype exports. The actual playable world
  scenes still use procedural placeholder geometry.
- The audiometric protocol (`TrialPlan`) is a simplified one-trial-per-frequency stand-in, not
  the planned descending-staircase protocol.
- The results screen uses placeholder "ear age" framing text; the real norm curve does not
  exist yet.
- Headphone vs Speaker is only ever user-selected, never auto-detected (deliberately — see
  `docs/13-open-questions.md`).
- Colors come from `Core/Shell/VisualTokens.cs`, which mirrors
  `docs/14-visual-bible.md` / `Art/Reference/brand/hear-visual-tokens.json` (v0.2
  handoff): Pearl/Ink neutrals, the Aurora accent palette, the 8-based spacing scale, radius
  tokens, semantic type sizes, motion-duration tokens, and the compact(<600)/medium(600-999)/
  wide(>=1000) breakpoints. All shell screens (splash, world selector, headphone choice,
  micro-instruction, HUD, results, settings) and the responsive nav now consume these tokens
  instead of ad hoc colors/sizes.
- UI Toolkit logs a harmless "No Theme Style Sheet" warning at runtime (no default theme asset
  was created) — layout works, but built-in control chrome (e.g. `Button` borders) is unstyled.
- The headless CLI integration-proof runner (`HearAppIntegrationProofCli`) reliably builds and
  starts, but `EditorApplication.EnterPlaymode()` did not reliably pump the Play-mode update
  loop under `-batchmode -nographics` in this environment (a known rough edge without the
  Unity Test Framework's PlayMode test runner) — two attempts (60s and 180s timeouts) both hung
  during a benign engine-internal search-indexing step and never reached our code. The
  interactive path was verified instead: the built macOS app boots cleanly to the World
  Selector with no runtime exceptions, and the Integration Proof logic itself is exercised by
  the exact same `TrialEngine.ProcessTrial` method used by the real session loop, so correctness
  rests on that shared code path rather than on this CLI convenience wrapper.

## Recommended next steps

1. Add the Unity Test Framework package and convert `HearAppIntegrationProofCli` into a real
   PlayMode test for CI, instead of a manual/CLI command.
2. Validate Android, iOS and macOS behavior on real target devices.
3. Replace `TrialPlan`'s simplified schedule with the real descending-staircase protocol.
4. Begin the real art pipeline (Phase E) only after this architecture is reviewed/approved —
   replace placeholders incrementally, world by world.
5. Resolve the open product questions in `docs/13-open-questions.md` (typography, exact
   breakpoints, Companion design, cultural direction for Paper Garden/River Journey) before
   investing in production art for those worlds.
