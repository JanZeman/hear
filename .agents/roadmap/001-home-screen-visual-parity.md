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
      on-device.
- [ ] Side-by-side screenshot of the running app vs. `sources/HEAR-App-UI-Home.png` shows no
      structural difference (layout, carousel, companion placement, nav, brand).
- [ ] The issues listed in `Hear/docs/v1.0-home-handoff/current-implementation-delta.md` are
      confirmed resolved on-device.

## Notes

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
