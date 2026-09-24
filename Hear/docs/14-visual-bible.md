# HEAR Visual Bible v0.1

**Status:** implementation baseline for the current prototype phase  
**Product:** HEAR  
**Claim:** **Sound opens worlds.**  
**Scope:** global brand, shell UI, responsive behavior, motion language, Companion rules, and the visual contract shared by all game worlds.

This document expands `04-brand-visual-language.md`. If the two conflict on visual implementation details, this document takes precedence for the current prototype phase. It does **not** freeze final production typography, exact color calibration, or final production artwork; those may be optically tuned later without changing the underlying system.

---

## 1. Design intent

HEAR should feel:

- light rather than clinical,
- playful rather than childish,
- highly professional without feeling corporate,
- modern and Scandinavian in restraint,
- visually confident with little ornament,
- welcoming to adults while leaving room for whimsical worlds,
- driven by sound, curiosity, discovery and transformation.

The product should never visually present itself as a hospital test, diagnostic tool, hearing-aid configurator or children's app.

### Core brand sentence

**Small signals open large experiences.**

The logo, motion, world transitions and interaction hierarchy should reinforce this idea without literally drawing ears or speakers everywhere.

### Primary rule

> **The shell is consistent. The worlds are free.**

HEAR can contain a comic 2D harbor, a Japanese paper theatre and a cinematic 3D river without losing product identity. Consistency comes from typography, spacing, interaction patterns, navigation, logo, motion language and result presentation — not from forcing every world into the same art style.

---

## 2. Brand architecture

HEAR has three related but separate identity layers.

### 2.1 Static identity — logo

The static HEAR mark is quiet, abstract and timeless.

It contains four visual elements aligned with the four letters `H E A R`:

1. **H:** small dark dot,
2. **E:** small pale ellipse,
3. **A:** medium blue ellipse,
4. **R:** large aurora blue-violet ellipse.

The four elements should read as a progression from a tiny signal to an opened space.

Do not add speaker cones, ear outlines, audio-frequency graphs or generic volume glyphs to the static logo.

### 2.2 Motion identity — opening / waves

When the logo moves, its ellipses may:

- emerge from the dot,
- expand,
- gently open into partial arcs,
- create a portal-like transition,
- become more saturated,
- transition into a world,
- eventually give physical volume to the final ellipse.

This is where the more sound-like wave language belongs. A static HEAR logo should not become a generic speaker icon.

### 2.3 Character identity — Companion

The Companion is **not the logo**.

It is a physical, character-like interpretation of the final ellipse and may be revealed later in the experience.

The first screen may show only the abstract logo. The user does not need to know immediately that the final ellipse contains the visual DNA of a future character or of modern hearing-instrument silhouettes.

The Companion may later appear as if the final ellipse has gained volume and personality.

---

## 3. Logo specification

### 3.1 Geometry

Use the supplied SVGs in `assets/brand/` as geometry references.

Required construction:

```text
    •        ()        (  )        (    )
    H         E          A            R

             Sound opens worlds.
```

The centers of the four visual elements should optically align with the centers of the four letters.

Optical alignment takes precedence over mathematically exact alignment when the chosen typeface requires it.

### 3.2 Dot

The first element is intentionally different from the other three:

- small,
- dark,
- solid,
- visually decisive.

Do not turn it into another pale ellipse. The dot creates the beginning of the story: almost nothing becomes something larger.

### 3.3 Ellipses

The next three elements:

- remain closed in the static mark,
- grow progressively,
- use a very soft dimensional/gradient treatment,
- avoid obvious glassmorphism or glossy skeuomorphism,
- remain readable in monochrome.

### 3.4 Clear space

Recommended minimum clear space around the full logo lockup: at least the visual height of the `H` on every side.

For the mark without wordmark, use at least half the diameter of the largest ellipse as clear space.

### 3.5 Minimum sizes

Prototype guidance:

- Full lockup with claim: avoid below ~180 logical px width.
- HEAR wordmark + mark without claim: avoid below ~96 logical px width.
- Mark-only icon: must remain recognizable at 32×32 logical px.

At very small sizes, simplify gradients before changing geometry.

### 3.6 Monochrome

A single-color version must exist for:

- legal/system contexts,
- high-contrast mode,
- printing,
- unavailable gradient rendering,
- small icons.

Recommended monochrome colors:

- dark: Ink 900,
- light: Pearl 0.

---

## 4. Color system

These values are **provisional implementation tokens**, chosen to match the selected Pure/Aurora direction. They can be optically tuned later; use tokens rather than hardcoded component colors.

### 4.1 Neutrals

| Token | Hex | Usage |
|---|---|---|
| `Pearl-0` | `#FCFCFF` | main light background |
| `Pearl-50` | `#F5F6FB` | elevated/background surfaces |
| `Mist-100` | `#ECEEF5` | subtle separators, disabled surfaces |
| `Slate-400` | `#8B93A7` | secondary text/icons |
| `Ink-700` | `#33405B` | secondary dark content |
| `Ink-900` | `#17223C` | primary text, first logo dot |

Pure white may be used inside a world when required, but shell white should usually be slightly softened (`Pearl-0`) to avoid a sterile medical feel.

### 4.2 Aurora palette

| Token | Hex | Usage |
|---|---|---|
| `Aurora-Pearl` | `#E9F3FF` | small ellipse / quiet tint |
| `Aurora-Sky` | `#A8D9F2` | medium ellipse / soft accents |
| `Aurora-Blue` | `#6DA8E8` | active controls / transitions |
| `Aurora-Iris` | `#8C79E8` | large ellipse / expressive accent |
| `Aurora-Violet` | `#6957D8` | saturated event accent |

### 4.3 Default aurora gradient

Recommended prototype gradient:

```text
Aurora-Pearl → Aurora-Sky → Aurora-Iris
```

Do not use the full saturated gradient everywhere. In normal shell UI, most surfaces stay neutral and the aurora treatment is an accent.

### 4.4 Saturation as behavior

Default state:

- pale,
- calm,
- low saturation.

During transitions, confirmation, world entry or meaningful success:

- saturation may temporarily increase,
- violet may become more visible,
- color may travel through the ellipses.

Color should feel like the interface briefly becoming alive rather than permanently glowing.

### 4.5 World colors

World art is not required to use the aurora palette.

Instead, shell overlays should remain legible over any world through:

- neutral translucent surfaces,
- controlled scrims,
- local contrast,
- adaptive foreground dark/light choice.

Do not recolor entire worlds to match the HEAR logo.

---

## 5. Typography

### 5.1 Direction

Typography should be:

- geometric/humanist sans,
- clean,
- open,
- calm,
- highly legible,
- not overtly futuristic,
- not rounded to the point of becoming childlike.

### 5.2 Prototype font

Recommended prototype family: **Inter** (or the closest available equivalent in the repository).

If Inter is not already available, do not block architecture work to obtain it. Use a neutral sans-serif placeholder and keep typography referenced through semantic styles/tokens so the family can be swapped later.

Do not embed a random system-specific font that would make macOS and Windows differ unintentionally.

### 5.3 Semantic styles

Suggested logical styles:

- `Display`: world title / major first-screen statement
- `Title`: page/sheet title
- `Headline`: card title
- `Body`: standard explanatory text
- `BodyStrong`: emphasized body text
- `Caption`: metadata / helper information
- `Label`: button/nav labels
- `Micro`: developer/debug only; avoid in production UX

### 5.4 Sizing guidance

Use logical size tokens, not fixed device pixels.

Prototype starting points:

| Style | Size | Weight |
|---|---:|---:|
| Display | 36 | 600 |
| Title | 28 | 600 |
| Headline | 20 | 600 |
| Body | 16 | 400 |
| BodyStrong | 16 | 600 |
| Caption | 13 | 400/500 |
| Label | 15 | 600 |

Scale may vary at responsive breakpoints, but typography should not continuously shrink simply because the viewport becomes narrow.

---

## 6. Spacing, shape and elevation

### 6.1 Spacing scale

Use an 8-based rhythm with a small 4 unit exception:

```text
4, 8, 12, 16, 24, 32, 48, 64
```

Prefer fewer strong spacing relationships over many arbitrary values.

### 6.2 Corner radii

Suggested tokens:

- `Radius-S`: 10
- `Radius-M`: 16
- `Radius-L`: 24
- `Radius-XL`: 32
- `Radius-Pill`: 999

World preview cards may use `Radius-L` or `Radius-XL`.

Buttons should feel soft, not toy-like. Avoid excessive pills for every component.

### 6.3 Borders

Borders are subtle:

- 1 logical px equivalent,
- low-contrast neutral,
- often unnecessary when elevation or surface contrast is sufficient.

### 6.4 Shadows

Use restrained soft elevation.

Avoid heavy black drop shadows and neumorphism.

Prototype guidance:

- small cards: soft shadow, low opacity, modest blur,
- modal/sheet: stronger but still diffuse,
- logo ellipses: subtle dimensional shading allowed.

---

## 7. Iconography

Icons should be:

- simple,
- rounded/geometric,
- consistent stroke weight,
- recognizable at small sizes,
- visually secondary to world artwork.

Avoid mixing emoji with product icons in production UI.

Do not use literal ear icons as the primary recurring symbol of HEAR. An ear icon may be used only when it is genuinely the clearest local instruction.

For left/right use explicit `L` and `R` labels when accuracy matters. Do not rely on color alone.

---

## 8. Global shell structure

The shell owns:

- splash,
- world selection,
- navigation,
- output/headphone setup,
- common instructions,
- settings,
- results,
- common dialogs/sheets,
- product branding.

The shell should occupy as little visual attention as practical once gameplay begins.

### 8.1 Navigation destinations

Initial shell navigation can expose:

- Worlds / Home,
- Results,
- Settings.

Avoid creating navigation items simply to fill a standard app pattern.

### 8.2 Desktop navigation

- **Wide:** left sidebar, icon + text.
- **Medium:** narrow left icon rail.
- **Compact / portrait-like desktop window:** bottom navigation.

The macOS/Windows native application menu may contain platform-standard actions but must not replace primary in-app navigation.

### 8.3 Mobile navigation

Compact mobile layouts may use bottom navigation where needed.

During gameplay, hide or minimize persistent navigation if it distracts from listening. A pause/exit control is enough if the world is otherwise immersive.

---

## 9. Splash and first impression

### 9.1 Splash

Target perceived duration: roughly 0.5–1 second when loading permits.

Sequence:

1. dark dot appears,
2. three ellipses emerge/expand,
3. `HEAR` resolves,
4. optional brief `Sound opens worlds.`,
5. transition opens into the active world selector.

No tutorial copy on splash.

### 9.2 World selector is the first real screen

The first meaningful screen should create desire, not ask for data.

Do not begin with:

- account creation,
- health questionnaire,
- permissions unrelated to immediate play,
- long tutorial,
- headphone warning,
- clinical explanation.

The user should see worlds immediately.

---

## 10. World carousel / home

### 10.1 Purpose

The home screen is the main “wow” screen and the visual promise of HEAR.

### 10.2 Cold start behavior

- World order remains stable.
- On cold start, select a random active world index.
- On return from gameplay, preserve the currently selected world.

### 10.3 Active world

The active world:

- owns the largest preview region,
- influences the full-screen/background atmosphere,
- may have subtle live ambient motion,
- shows world title,
- provides a clear play/enter affordance.

Do not show implementation terms `2D`, `2.5D`, `3D` to users.

### 10.4 Neighboring worlds

Neighbor cards/previews should make horizontal exploration obvious without visual clutter.

Depending on viewport:

- portrait: mostly one world with hints of neighbors,
- square: one dominant world plus stronger neighbor hints,
- wide desktop: multiple worlds may be visible while one remains clearly selected.

### 10.5 Background transition

When changing selected world:

- do not simply swap a flat background instantly,
- crossfade/transform atmospheric colors and world extension art,
- keep HEAR shell elements stable,
- motion target: roughly 350–600 ms for the main visual transition,
- avoid large motion that could cause discomfort.

The shell should feel like a stable gallery while the worlds move behind it.

---

## 11. Headphone / output choice

Only ask after the user has selected a world.

Recommended hierarchy:

**Headphones recommended**  
Short reason: left and right can be tested separately / more precise experience.

Primary choice:

- Continue with headphones

Secondary choice:

- Play through phone/computer speakers

Do not visually imply that speaker mode is invalid or a failure state.

### 11.1 Headphone setup visual language

This is a suitable moment for the brand ellipses/Companion to become slightly more physical.

Potential reveal:

- final ellipse separates,
- gains volume,
- becomes the Companion,
- left/right may be represented by mirrored orientation or a pair,
- use explicit L/R labeling.

Do not reveal stimulus timing through the Companion.

---

## 12. Instructions

Prefer contextual micro-instruction to tutorial pages.

Example:

> Tap when you hear the tone.

Rules:

- one primary action per instruction,
- show at moment of need,
- disappear after demonstrated understanding,
- do not explain game-world fiction separately unless necessary,
- no mandatory multi-page onboarding before play.

---

## 13. Gameplay shell

Gameplay should maximize world presence and listening focus.

Allowed shared elements:

- pause/exit,
- subtle session progress,
- optional accessibility control when necessary.

Avoid persistent:

- score chrome unless the world genuinely needs it,
- large navigation bars,
- decorative audio meters,
- animated UI unrelated to user action.

### 13.1 Session progress

Progress is session progress, not hearing performance.

It should visually communicate “how far through the experience am I?” without implying that missed tones are game failure.

Possible forms:

- quiet dot sequence,
- slim progress track,
- world-integrated progress if semantically safe.

The global shell should provide a neutral fallback.

---

## 14. Results

Results are presented in HEAR shell language, independent of the world that was played.

The result screen should feel:

- calm,
- clear,
- nonjudgmental,
- not gamified into shame/reward,
- visually linked to the aurora system.

Do not make medical/diagnostic claims unless separately validated by product/science decisions.

The world may appear as a small memory/thumbnail, but results hierarchy belongs to the shell.

---

## 15. Companion specification

### 15.1 Character goals

The Companion should feel:

- curious,
- friendly,
- intelligent,
- quiet,
- slightly mysterious,
- not infantile,
- not ghostly/scary,
- not fecal/organic in an unfortunate way.

### 15.2 Base material/color

Base:

- pearl/light body,
- faint aurora blue-violet tint,
- gentle edge/shadow definition.

Avoid dark brown, muddy gradients or featureless white ghost styling.

### 15.3 Shape

- derived from the largest logo ellipse,
- lightly asymmetrical is acceptable,
- may subtly evoke an archetypal modern hearing-instrument silhouette,
- never reproduce identifiable product hardware,
- no receiver wire, microphone ports, buttons or brand-specific geometry by default.

### 15.4 Face

Use minimal facial information.

Eyes may exist but must not dominate the concept.

Listening should be communicated more through:

- body tilt,
- orientation,
- lean,
- subtle surface motion,
- tiny signal accents.

### 15.5 World adaptation

The Companion keeps recognisable silhouette/DNA but may adapt materially:

- Paper Garden: paper/shadow interpretation,
- Tide Troubles: physical character with small thematic prop if useful,
- River Journey: physical companion integrated into dock/boat environment,
- Space: weightless/aurora treatment.

Hard rule:

> **Logo never dresses for a world. Companion may.**

### 15.6 Easter-egg placement

The Companion can sometimes be embedded physically in the selected home-world preview rather than appearing as a fixed UI widget.

This encourages discovery and prevents it from becoming visual clutter.

Do not make finding the Companion necessary for gameplay.

---

## 16. Responsive composition

This section complements `05-responsive-platform-layout.md`.

### 16.1 Core Safe Square

Every world and major home composition must survive a central 1:1 viewport.

All gameplay-critical information belongs inside this core.

Extra width/height reveals atmosphere rather than essential actions.

### 16.2 Layout modes

Use conceptual modes rather than device names:

- `Compact`: narrow width / portrait-like,
- `Medium`: tablet, foldable, small desktop,
- `Wide`: desktop/landscape.

Exact breakpoints are implementation details and may be tuned from live testing. Initial engineering guidance is approximately:

- Compact: `< 600` logical width,
- Medium: `600–999`,
- Wide: `>= 1000`.

Do not derive everything from aspect ratio alone; actual logical width matters.

### 16.3 Continuous resizing

Desktop resize must be continuous. Avoid designs that only look correct at a few preset resolutions.

When crossing breakpoints:

- nav structure may change,
- text may reflow,
- cards may rearrange,
- secondary detail may hide.

Between breakpoints:

- proportional spacing/size changes should remain stable,
- world camera/art reveals more or less area.

### 16.4 Never distort

Never independently stretch X or Y world artwork.

Allowed:

- proportional scale,
- crop,
- reveal,
- reflow,
- reposition,
- camera reframing,
- alternate layout.

---

## 17. World asset-production contract

Production art should be delivered as a responsive construction set, not as screenshots for one device size.

### 17.1 General source canvas principle

When generating or painting source art:

- compose the important subject in the central square,
- include generous bleed left/right/top/bottom,
- avoid placing unique critical details at extreme edges,
- separate movable/animated subjects from background whenever practical.

### 17.2 Suggested raster master sizes

These are guidance, not runtime requirements.

For high-quality painted backgrounds, prefer a master large enough to crop across multiple ratios, e.g. roughly 3000–4000 px on the shorter dimension when source tooling permits.

Do not require every runtime asset to remain at master resolution. Import settings should generate appropriate platform/runtime sizes.

### 17.3 Layering

Prefer semantic layers:

```text
background
far environment
mid environment
gameplay subjects
foreground framing
FX / particles
UI (separate from world art)
```

### 17.4 Transparency

Characters, foreground props and parallax pieces should use transparent backgrounds where appropriate.

Avoid baked shadows when the object may move independently unless the art direction intentionally requires them.

### 17.5 Naming

Suggested convention:

```text
world_<world>_<layer>_<description>_<variant>
```

Examples:

```text
world_paper_bg_mountains_day
world_paper_fg_cherry_branch_left
world_tide_actor_fisherman_idle
world_river_prop_canoe_player
```

Use consistent lowercase snake_case or the repository's established convention; do not mix styles.

---

## 18. Three reference worlds — visual contracts

These are not production art specifications; they demonstrate allowed variation.

### 18.1 Tide Troubles — 2D comic action

Visual qualities:

- bright but tasteful,
- expressive silhouettes,
- fast readable motion,
- comic timing,
- humorous targets/actions,
- richer saturation than the shell.

World may use exaggerated motion and impact.

Do not turn global shell components into cartoon props.

Success event may be an automatic capture/hit/gag after a correct hearing response.

### 18.2 The Paper Garden — 2.5D paper theatre

Visual qualities:

- Japanese-inspired paper/layer theatre,
- warm tactile paper texture,
- layered silhouettes,
- modest parallax,
- crafted/lit-stage feeling,
- restrained palette with world-specific warm accents.

Build the scene from separate layers rather than one flattened image wherever parallax or independent animation is expected.

Success events may advance a small stage action/story beat.

### 18.3 River Journey — 3D cinematic journey

Visual qualities:

- cinematic yet stylized,
- warm natural light,
- convincing spatial depth,
- attractive water,
- environmental storytelling,
- scene detail increases as the journey approaches settlement.

Use real 3D composition for near/mid content; distant scenery may be optimized or faked when appropriate.

Correct detection may create a paddle stroke / water response / temporary forward impulse while session progression remains independent.

---

## 19. Motion language

Motion should communicate cause and continuity, not decorate every surface.

### 19.1 Timing tokens

Prototype starting points:

- `Motion-Instant`: 90–140 ms
- `Motion-Fast`: 180–240 ms
- `Motion-Normal`: 300–420 ms
- `Motion-Scene`: 450–650 ms
- `Motion-Ambient`: multi-second loops

### 19.2 Easing

Default UI movement:

- ease-out for entering/confirming,
- ease-in-out for spatial transitions,
- avoid strong elastic/bounce except inside a deliberately comic world.

### 19.3 Motion hierarchy

Most expressive to least expressive:

1. world-specific success event,
2. entering/changing world,
3. logo/Companion reveal,
4. navigation transitions,
5. ordinary buttons/settings.

Do not make ordinary shell button animation compete with a world.

### 19.4 Reduced motion

Architecture should make it possible to reduce nonessential motion later.

Do not make hearing-test semantics dependent on animated movement.

---

## 20. Accessibility and clarity

Even though HEAR is playful, UI should follow strong accessibility basics:

- important text maintains sufficient contrast,
- L/R is indicated with text/shape, not color alone,
- touch targets are generous,
- focus/keyboard navigation should be feasible on desktop,
- dynamic world backgrounds receive scrims when text contrast requires it,
- no essential instruction exists only as animation,
- avoid tiny text over cinematic backgrounds.

Desktop users should eventually be able to navigate shell UI with keyboard, even if gameplay input remains intentionally simple.

---

## 21. Dark mode

HEAR may support dark surfaces, but dark mode is not simply color inversion.

Recommended dark shell:

- background: deep Ink/navy rather than pure black,
- text: Pearl,
- aurora gradients retain blue-violet identity,
- cards: slightly lifted navy/slate surfaces,
- world imagery remains authored per world.

The light identity is the current primary design reference; dark mode may be implemented after core shell behavior is stable.

---

## 22. Do / do not

### Do

- let world art carry emotion,
- keep the shell calm,
- use generous whitespace,
- use aurora color selectively,
- keep brand geometry simple,
- preserve the dark starting dot,
- let the final ellipse become meaningful through motion later,
- reframe rather than distort,
- integrate the Companion into worlds when appropriate,
- make the first real screen visually desirable.

### Do not

- lead with an ear icon,
- make the UI look clinical,
- make every surface purple/blue,
- use a permanent audio waveform everywhere,
- turn the Companion into a literal real-world product,
- force every world into the same illustration style,
- stretch world artwork non-uniformly,
- front-load tutorials,
- use visual activity to reveal auditory stimulus timing,
- confuse session progress with hearing performance.

---

## 23. Unity implementation guidance

This bible intentionally does not mandate uGUI vs UI Toolkit. Inspect the existing project and choose the least disruptive approach that satisfies responsive behavior.

Regardless of implementation technology:

- centralize visual tokens,
- avoid hardcoded colors/sizes throughout scene scripts,
- expose world-specific presentation through world-owned assets/components,
- keep shell UI outside world-prefab assumptions,
- allow breakpoints/layout states to be tested in the editor,
- create a developer viewport/resizing workflow for portrait, square and landscape,
- preserve safe-area handling on mobile.

Useful test aspect ratios include:

- 9:16 portrait,
- ~1:1 foldable/square,
- 4:3,
- 16:9 landscape,
- wide resizable desktop,
- narrow portrait-like desktop window.

---

## 24. Authority and what remains open

### Considered decided for this phase

- name `HEAR`,
- claim `Sound opens worlds.`,
- four-element logo concept,
- dark dot + three growing ellipses,
- Pure/Aurora direction,
- static-logo vs motion-identity separation,
- Companion separate from logo,
- shell consistency vs world freedom,
- world carousel as first meaningful screen,
- responsive Core Safe Square principle,
- wide/medium/compact navigation behavior,
- no non-uniform world-art stretching.

### Still allowed to evolve

- exact typeface,
- exact optical spacing of the logo,
- final production gradient values,
- final icon set,
- final Companion proportions/face,
- final result visualization,
- final detailed art assets for all worlds,
- exact responsive breakpoint numbers,
- final dark-mode polish.

When an open detail blocks implementation, prefer a reversible tokenized/default implementation over inventing a permanent new visual rule.

---

## 25. Quick implementation checklist

Before calling the shell visually aligned with this bible, verify:

- [ ] HEAR logo uses 4 aligned visual elements: dot + 3 ellipses.
- [ ] Claim is `Sound opens worlds.`
- [ ] Shell uses restrained Pearl/Ink/Aurora tokens.
- [ ] UI is visually consistent across all three worlds.
- [ ] World carousel is the first meaningful screen.
- [ ] Active world changes background atmosphere.
- [ ] Random cold-start selected world does not reorder worlds.
- [ ] Headphone choice appears only after world selection.
- [ ] Compact desktop moves primary app navigation to bottom navigation.
- [ ] Medium desktop uses icon rail.
- [ ] Wide desktop uses sidebar.
- [ ] No world artwork is non-uniformly stretched.
- [ ] Central 1:1 Core Safe Square remains playable.
- [ ] Companion is separate from static logo.
- [ ] Companion remains light/pearl/aurora rather than dark muddy organic form.
- [ ] Sound-wave forms appear primarily in motion, not as generic static speaker branding.
- [ ] World success events may be visually expressive; shell stays calm.
- [ ] No visual element exposes auditory stimulus onset.
