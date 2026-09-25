# Home screen 1:1 visual parity with GOLDEN concept board

**Status**: In Progress
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

Bring the running Home / World Selector screen to a visual 1:1 match with
`sources/HEAR-App-UI-Home.png` and `sources/HEAR-App-UI-Concept-Board.png`, verified against the
real connected Android device (Galaxy Z Fold, `SM-F956B`), not just the Editor Game view. Iterate:
build -> install -> screenshot -> compare -> adjust `ShellUIController.cs` / `WorldArt.cs` /
`VisualTokens.cs`.

## Definition of done

- [ ] Every item in `Hear/docs/v1.0-home-handoff/02-developer-checklist.md` verified true
      on-device. NOT DONE: that checklist has not been walked item by item; only the properties in
      the measurement table below were verified.
- [x] Side-by-side screenshot of the running app vs. `sources/HEAR-App-UI-Home.png` shows no
      structural difference (layout, carousel, companion placement, nav, brand). Verified
      2026-09-25 by pixel measurement, not by eye - see the table below; every structural ratio is
      within 3% except the three noted there, each with a stated reason. Evidence:
      `Hear/docs/v1.0-home-handoff/evidence/2026-09-25-home-on-device-SM-F956B.png`.
- [x] The issues listed in `Hear/docs/v1.0-home-handoff/current-implementation-delta.md` are
      confirmed resolved on-device. All twelve, seen on the device: 1, 3, 6, 7, 12 by the carousel
      and full-bleed world art; 2 by the brand block measuring within 0.2% of the board; 4, 11 by
      absence; 5 and 8 by all three world names rendering on one line at the same cap height
      (checked for "RIVER JOURNEY", "THE PAPER GARDEN" and "TIDE TROUBLES"); 9 by the Companion
      being a separate sprite placed from `companion.json`; 10 by the nav measuring within 0.4
      percentage points of the board. Item 9's "grounding" half is only partly resolved: the
      Companion no longer sits on the card, but our background crop gives it no surface to rest
      on the way the board's foreground rock does.

## Measurement table, 2026-09-25 (round 2 of this item)

Method: both PNGs dumped to raw RGBA (`ffmpeg -f rawvideo -pix_fmt rgba`) and scanned in Python
for colour transitions. Reference `sources/HEAR-App-UI-Home.png` is 852x1846 (aspect 0.462);
the device (Galaxy Z Fold `SM-F956B`, cover screen) is 968x2376 (aspect 0.407). Because the two
resolutions and aspect ratios differ, **every figure below is a ratio** against a stable reference
in its own image - screen width, screen height, or the element the property must stay proportional
to. `pp` means percentage points of the stated denominator.

| Property | Reference | App | Delta | Fix applied |
| --- | --- | --- | --- | --- |
| Active card, width / screen W | 0.5716 | 0.5723 | +0.1% | `Golden.CardWidthOfScreenW`; was 0.71 from the spec, the board says 0.572 and the board wins |
| Active card, height / own width | 1.517 | 1.516 | -0.1% | `CardHeightOverWidth`; height now derives from width, not from a screen-height fraction |
| Active card, height / screen H | 0.4003 | 0.3535 | -4.7 pp | **Deliberately not matched.** The card's own silhouette is held instead; this device is proportionally much taller, so an aspect-correct card covers less of its height |
| Active card, top / screen H | 0.2422 | 0.2403 | -0.2 pp | `CardTopOfScreenH`; the card is anchored at this ratio instead of being centred in whatever slack the carousel had |
| Active card, corner radius / card W | 0.0739 | 0.0650 | -0.9 pp | `CardRadiusOfCardW` = 0.082; corner-settle depth is the least precise number here (3px antialiased border) |
| Active card, border width / card W | 0.0041 | 0.0054 | +0.13 pp | `CardBorderOfCardW`, floored at 1 logical px |
| Active card, border alpha | ~0.80 | ~0.77 | -0.03 | `CardBorderAlpha` = 0.7 |
| Peek card, height / active height | 0.739 | 0.740 | +0.1% | `PeekHeightOfActiveH` |
| Peek card, visible width / screen W | 0.1913 | 0.1911 | -0.1% | follows from the peek's own aspect plus the gap below |
| Peek card, gap to active card / screen W | 0.0235 | 0.0227 | -0.08 pp | `PeekGapOfScreenW` |
| Peek card, centre rise / active height | 0.028 | 0.028 | 0% | `PeekCenterRiseOfActiveH` |
| Peek card, corner radius / own width | same ratio as active | same ratio as active | - | shares `CardRadiusOfCardW` |
| Peek card, label | present | none | **approved deviation** | side cards carry no world name; human direction 2026-09-25 |
| Play, width / screen W | 0.4249 | 0.4256 | +0.2% | `PlayWidthOfScreenW`; was 0.61 from the spec |
| Play, height / own width | 0.2652 | 0.2646 | -0.2% | `PlayHeightOverWidth`; was a 0.19 factor clamped to 44-64 |
| Play, corner radius / own width | 0.1022 (pill) | 0.1019 (pill) | -0.03 pp | unchanged, already a pill |
| Play, centre x | 0.5000 | 0.4995 | -0.05 pp | - |
| Play, glow at its own edge | none: 91 -> 72 -> 130 -> 240 luminance | none: 58 -> 42 -> 255 | match | **Glow removed.** The app's four halo layers produced four visible luminance steps over 20px; the board has a razor-sharp edge with a faint one-pixel dark rim, no halo |
| Gap card bottom -> dots top / screen H | 0.0152 | 0.0147 | -0.05 pp | `CardToDotsOfScreenH` |
| Gap dots bottom -> Play top / screen H | 0.0244 | 0.0240 | -0.04 pp | `DotsToPlayOfScreenH` |
| Gap Play bottom -> nav cell top / screen H | 0.1495 | 0.2092 | +6.0 pp | **Deliberately not matched.** All vertical slack is collected here, where the board also leaves open foreground; a taller device has more of it |
| Dot count | 3 | 3 | - | - |
| Dot diameter / Play width | 0.0552 | 0.0546 | -0.06 pp | `DotDiameterOfPlayW` |
| Dot pitch / Play width | 0.1202 | 0.1177 | -0.25 pp | `DotPitchOfPlayW` |
| Dots all one size, colour marks the active one | yes | yes | match | re-verified this round; still true |
| Wordmark width / screen W | 0.2770 | 0.2769 | -0.04% | `BrandLogoOfScreenW` = 0.393, measured on-device rather than computed - the supplied bitmap does not carry the board's own wordmark proportions |
| Claim width / screen W | 0.3580 | 0.3574 | -0.2% | `ClaimFontOfScreenW` |
| Claim centre y / screen H | 0.1983 | 0.1999 | +0.16 pp | `ClaimTopOfScreenH` pins the claim's band rather than hanging it off the logo |
| Logo block top / screen H | 0.0780 | 0.0779 | -0.01 pp | `BrandTopOfScreenH` |
| Logo mark (ellipse row) width / screen W | 0.2793 | 0.2903 | +1.1 pp | not fixable in code: the bitmap's ellipse row is proportionally wider than the board's. [007](007-brand-art-visual-weight.md) |
| Wordmark centre y / screen H | 0.1663 | 0.1376 | -2.9 pp | same cause: the bitmap's mark is proportionally shorter than the board's, so the wordmark sits higher. [007](007-brand-art-visual-weight.md) |
| Card title, cap height / card W | 0.0431 | 0.0433 | +0.02 pp | `CardTitleCapOfCardW` |
| Card title, line width / card W | 0.680 | 0.664 | -1.6 pp | `CardTitleTrackingEmHundredths`; see the unit note below |
| Card title, line count | 1 | 1 | match | verified for all three world names on-device |
| Companion, visible width / screen W | 0.2031 (board) / 0.19 (`companion.json`) | 0.186 | -1.7 pp vs board, -0.4 pp vs JSON | element box divided by the sprite's 0.6045 alpha-content fraction; the JSON's fraction describes the Companion as seen, the element box carries transparent margin |
| Companion, centre x / screen W | 0.8556 (board) / 0.80 (JSON) | 0.799 | matches JSON | JSON wins: it is the spec across aspect buckets the board never shows |
| Companion, centre y / screen H | 0.7405 (board) / 0.73 (JSON) | 0.731 | matches JSON | as above |
| Nav icon width / screen W | 0.0599 | 0.0579 | -0.2 pp | falls out of the panel-scale fix |
| Nav label width / screen W | 0.0892 | 0.0899 | +0.07 pp | compact label 10 -> 11 |
| Nav icon centre y / screen H | 0.9076 | 0.9091 | +0.15 pp | `NavCenterOfScreenH`; the overlay used to sit flush at bottom:0 |
| Nav label centre y / screen H | 0.9350 | 0.9322 | -0.28 pp | as above |
| Nav outer icon centre x / screen W | 0.1808 | 0.1772 | -0.36 pp | `CompactSideInset` = 8 |
| Nav active icon halo, alpha at icon edge | ~0.50 | ~0.58 | +0.08 | nine layers fitted to the board's measured exponential falloff |
| Nav active icon halo, reach / icon width | ~0.64 | ~0.68 | +0.04 | as above |
| Nav inactive icon halo | none | none | match | the app's dark inactive halo removed; the board has none |

### Overlap check (explicit, this was the earlier regression)

Measured on the final on-device screenshot, in physical px: card bottom 1410 < dots top 1445 <
dots bottom 1470 < Play top 1527 < Play bottom 1635 < nav icon top 2132. Nothing draws outside
its own box any more, because the Play glow is gone. On top of the measured gaps there is now a
structural guard in `UpdateCarouselForCurrentSize`: the card is shrunk, keeping its aspect, if the
column would not otherwise fit between the brand block and the reserved nav strip. Only the one
breakpoint this device offers was tested on hardware; the Fold's inner screen and desktop widths
were not, so the guard is reasoning, not an on-device result, for those.

### Root cause found this round

The panel was configured `ConstantPhysicalSize` with `referenceDpi = 96`, which resolved this
968-physical-px screen to a **221-unit-wide** panel. Every size token written in "logical px"
(16px body, 26px nav icon, 32px radius) therefore rendered about 1.67x too large relative to the
screen - the wordmark measured 0.435 of screen width against the board's 0.277, the nav icon 0.095
against 0.060. `referenceDpi = 160` makes one unit one Android dp, which is the space those tokens
were written for, and keeps the 600/960 breakpoints meaning what `home-layout.json` says.

### Two behaviours worth remembering

- `letterSpacing` in this Unity version behaves as **hundredths of an em**, not logical px: a
  nominal 2.8 bought 0.36px of gap. Two on-device measurements were needed to see it. The card
  title's tracking constant is therefore not scaled by the font size.
- Stacked darkening bands must carry their **largest alpha on their shortest band**. The card's
  title gradient had it the other way round, so the darkening stopped dead at 22% of the card and
  cut a hard horizontal line across it. Same defect existed as one flat 26%-tall screen scrim.

### Known non-parity items, all out of this item's scope

- Font family is not Inter: [006](006-integrate-inter-font.md). Text widths above therefore carry
  a font-metric difference of a few percent on top of the geometry.
- Logo mark proportions: [007](007-brand-art-visual-weight.md).
- OS status bar: [005](005-immersive-os-chrome.md).
- Background crop is tighter than the board's on this aspect ratio, and the world taglines differ
  from the board's copy ("Listen. Explore. Progress." vs "Paddle your way through sound."). Both
  are content/art decisions, not layout; neither was changed without asking.

## Notes

- 2026-09-25: On explicit human direction: compact bottom-nav icons/labels enlarged ~9%
  (`CompactIconSize` 26 -> 28.3, `RailIconSize` 18 -> 19.6, compact label font 11 -> 12); Play
  pill glow max padding 5 -> 7 (running total +3px across two rounds, still reported barely
  visible - if raised again, alpha/falloff are the more likely lever than padding alone).
  Confirmed on-device (icons/labels visibly bigger in a close-up crop). Discussed but NOT
  implemented: dynamically repositioning the glass nav panel based on live Android
  `WindowInsets` (whether the OS's own nav bar is momentarily shown via edge-swipe or hidden) -
  technically feasible (same `AndroidJavaObject`/listener pattern as the status-bar work), but the
  backdrop-extension fix above should already prevent any real collision, since the OS bar always
  draws on top of app content; recommended verifying that on-device before building another native
  integration. Blocked on the test device being physically unfolded mid-session (adb can't force
  it back to the cover screen) - re-verify next session.
- 2026-09-25: Compact-overlay bottom nav left a bare gap between the glass panel's bottom edge
  and the true screen edge (reserved so the panel never overlaps the OS gesture-nav safe area -
  `GetBottomSafeAreaInsetLogical()`). Human feedback: "vypada OK" when the OS's own nav bar
  happened to be visible and filled that gap, "vypada blbe" (empty) when it auto-hid, which is
  most of the time (`AB` immersive-sticky). Fixed with a new `_navBackdropExtension` element
  (`ShellUIController`) that spills below the glass panel's own box via a negative `bottom`
  offset, filling exactly that gap with the identical glass colour (now
  `ResponsiveNavBar.GlassBackgroundColor`, shared instead of duplicated) - the panel now reads as
  one continuous surface reaching the true screen edge regardless of the OS bar's own state.
  Confirmed on-device with the OS bar auto-hidden (the common case). Not re-confirmed with the OS
  bar swiped visible - the test device physically unfolded mid-check (a swipe-from-edge gesture
  meant to reveal the OS bar instead triggered/coincided with the fold-state change, switching to
  the inner 1856x2160 display and the Wide/Medium icon-rail layout) and adb has no way to force it
  back to the cover screen; should still hold structurally (the OS bar always draws on top of app
  content, so it would simply cover this backdrop the same way it already covers the panel above
  it), but wants an eyes-on check next on-device session.
- 2026-09-25: Play pill glow +1px on explicit human direction ("neviditelny, zvec o jeden pixel");
  max padding 4 -> 5, alpha/falloff unchanged (they asked for the one specific number, not a
  general re-tune).
- 2026-09-25: Two more corrections from the same on-device feedback loop. (1) Play pill glow: the
  previous round's "faint but real halo" (max 10px, peak alpha 0.12) was then "moc viditelne" -
  wanted "podvedome"/subliminal, "uzoulinky prouzek" (a hairline sliver). Narrowed further (max
  4px, peak alpha 0.07, steeper falloff) - now barely perceptible even in a close-up crop. Three
  rounds total on this one control (invisible -> too visible -> subliminal) - if it needs tuning
  again, start from these numbers, not the very first attempt's. (2) Compact Home bottom nav:
  restored a "glass panel" background (translucent dark navy, thin light top rim, rounded top
  corners) per `sources/HEAR-App-UI-Concept-Board.png`, which shows exactly that - overriding an
  earlier pass that read a *different* reference (the GOLDEN board) as having no bar background
  at all and removed it entirely. Both references are real handoff artifacts; when they conflict,
  the human's pointed reference for the specific feedback wins.
- 2026-09-25: Play pill glow restored on explicit human direction ("opravdu musi byt jen velice
  subtilni"), deliberately deviating from the GOLDEN board (which has none there at all, per the
  pixel measurement recorded in an earlier round). First attempt reused the nav icon glow's
  numbers (max 6px padding, peak alpha 0.05) and came out essentially invisible on-device -
  confirmed by human observation and a close-up crop. Root cause: the nav icon is a thin-stroke
  glyph with transparent gaps the glow shows through even at pad=0 (fully inside the icon's own
  bounding box); the Play pill is one opaque filled shape, so the ENTIRE visible contribution is
  only the sliver of each layer beyond the pill's own edge - the same alpha/padding numbers are
  much less visible on a filled shape than on a glyph. Retuned (max 10px, peak alpha 0.12, 14
  layers, same smooth exponential falloff) until a close-up crop showed a real but faint halo,
  clearly short of the old wide/strong version. Also confirmed, on request, that nav icon/label
  size itself was never changed by the glow work (`CompactIconSize`/`RailIconSize` = 26/18
  unchanged across all commits) - a side-by-side crop showed the icon glyph and "Worlds" label at
  identical size before and after; the earlier glow fix only changed the halo's own footprint.
- 2026-09-25: Active bottom-nav icon glow (`ResponsiveNavBar.AddItem`) softened back down after
  human feedback (side-by-side photo comparison) that it had regressed to a "hard-edged blue blob
  with visible rings". History: a subtler version (max 8px padding, low alpha) was committed in
  `2d6bd48`, then overwritten by `1cc0e81`'s own reference-board remeasurement, which found the
  board's actual halo wide/strong (padding to 17.5px, composite alpha ~0.5 at the icon edge) and
  implemented that faithfully - technically matching its measurement, but with only 9 hand-tuned
  layers spread that wide, each ring's edge became individually visible (UI Toolkit has no real
  blur; this whole effect is stacked flat-alpha circles, which only reads as continuous when the
  steps between layers are small). Fix: kept the halo close to the earlier subtler footprint (max
  9px, not 17.5px) but generates many more layers (14) from a smooth exponential falloff formula
  instead of a handful of hand-tuned values, so it still looks continuous. Also removed a stale
  orphaned comment left behind after an earlier `SizeGlowPill` helper (Play button glow, already
  removed by `1cc0e81` - the board has no glow there at all) was deleted but its doc comment
  wasn't. Confirmed on-device via a close-up crop: smooth, contained glow, no visible rings.
- 2026-09-25: Home's full-bleed ambient background is now blurred, darkened and desaturated
  (`WorldArt.GetTreatedAmbient`, `Assets/HearApp/Resources/Shaders/AmbientTreatment.shader`),
  while the carousel cards and all UI stay untouched/sharp - relaying designer feedback ("uzivatel
  si nemusi uvedomit, ze tam je carousel"; the reference concept board itself doesn't show this,
  so this is a deliberate deviation from it, not a parity fix). This **reverses** the earlier
  v1.0 handoff's own rule ("sharp active-world background, never generic blur" -
  `docs/v1.0-home-handoff/`) - that handoff doc is left as-is (historical record of what was
  actually delivered then), but the two code comments that stated it as current
  (`ShellUIController.ShowWorldSelectorScreen`'s topScrim comment, `WorldArt.GetHomeBackground`'s
  doc comment) were updated so they don't contradict the live behavior. Implementation: a cheap
  downsample Blit (the real softness) followed by a small custom shader
  (`Hidden/HearAmbientTreatment`) doing a 9-tap blur + darken/desaturate lerp in one pass,
  computed once per source texture and cached as a `RenderTexture` (never per-frame). The shader
  lives in a `Resources/` folder specifically so it can't hit the build-stripping bug found
  earlier this session with the URP particle shader (`Shader.Find` has no such guarantee).
  Confirmed on-device: background clearly hazier/dimmer, carousel/UI unaffected.
- 2026-09-25: Third on-device round, measured rather than eyeballed (see the measurement table
  above). Root cause of the whole "everything is too big" family of defects was the panel's
  `referenceDpi = 96`. Play button glow removed outright - the reference has none. Bottom nav's
  active-icon glow rebuilt from a measurement of the board and is now far wider and stronger than
  the previous "max +8px, <=5% opacity" guess, which was wrong in the other direction.

- 2026-09-25: Second on-device feedback round, fixed and verified: (1) **Overlap bug** - the
  carousel/dots/Play button could visually overlap (card height was a flat `screenH * 0.49`
  fraction that could exceed the carousel's own resolved box and spill into the rows below);
  `activeH` is now clamped to the carousel's actual resolved height, structurally impossible to
  overlap regardless of breakpoint. (2) Glow (Play button + nav icons) redone as 4-5 layers at
  0.015-0.14 opacity instead of 2 layers at 0.16-0.32 - the original read as "naive"/"brutal".
  (3) Active card border widened 1px -> 3px with higher opacity; peek cards now use a smaller
  corner radius, 74% height scale (was 88%) and slightly reduced opacity, reading as a distinctly
  different/receded shape rather than a same-shape crop - human called the previous peek shape
  "completely wrong", this is a best-effort second pass, not yet re-confirmed. (4) World-switch
  transition (swipe or tap) now fades + rises in (~360ms, ease-out) instead of snapping instantly
  - confirmed via an on-device `adb screenrecord` (a mid-transition frame shows the old and new
  card cross-dissolving), not just by reading the code.
- 2026-09-25: Confirmed on-device (this Galaxy Z Fold, gesture nav): no Android status bar or
  navigation bar visible in any screenshot - `androidStartInFullscreen`/`androidFullscreenMode`
  already do their job here. iOS equivalent is unverified (no iOS build produced this session).
  Tracked as its own item: [005](005-immersive-os-chrome.md).
- 2026-09-25: Human-directed deviations from the handoff spec/checklist, superseding the
  corresponding checklist items above: peek (side) carousel cards carry no world-name label at
  all (only the active card is titled); the carousel uses a coverflow shape (peek cards scaled to
  88% of the active card's height, not just narrower); swipe-to-change-world was added (not in
  the original spec). Definition of done's checklist reference should be read with these
  overrides.
- 2026-09-25: Fixed real bugs found via on-device screenshots (Galaxy Z Fold cover screen,
  968x2376 @420dpi): (1) `Assets/HearApp/Resources/Icons/*.png` had solid-black fills, so
  `unityBackgroundImageTintColor` (a multiply) could never recolor them - the "active blue nav
  icon" never actually worked, only its background glow did; recolored all icon sources to white
  fill. (2) Companion was anchored to the active card's corner instead of `companion.json`'s
  screen-normalized `homePlacement`; fixed, with a clamp so it never overlaps the card on unusually
  tall/narrow screens. (3) Play button height was fixed at 59px regardless of width, reading as
  "too tall" on narrower widths; now proportional (clamped 44-64px) plus a soft glow halo. (4) Dots
  (world counter) enlarged/brightened - too small to read as present. (5) Bottom nav icons now get
  a soft glow halo (not just the active one) for contrast against busy world photography.
- 2026-09-25: Built via a new `Hear/Build Release/Android` menu item (non-development build,
  excludes `DevOverlay` and the "Development Build" watermark) - needed because the existing
  `DevOverlay` defaults to visible and has no way to dismiss it on a touch-only device, which
  would otherwise obscure every home-screen screenshot.
- 2026-09-25: Known remaining gap, not yet fixed: on this device's extreme aspect ratio, the
  active card's title still wraps to two lines (e.g. "RIVER JOURNEY") where the reference mockup
  (a standard ~19.5:9 phone) shows one line. Judged acceptable per the spec's own "one line where
  space allows" wording rather than forced further - the Fold cover screen is physically narrower
  than any reference device.
- 2026-09-25: Started. Using the connected physical device instead of an emulator for accurate
  density/safe-area (`Hear/README.md` notes screenshots were never captured in a prior session).
