# Reduce visual weight of the logo mark and re-check other brand/UI art

**Status**: Done
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

Human feedback 2026-09-25: "Elipsy loga jsou moc tlusté" (the logo's ellipses are too heavy/bold).
`hear-logo-on-dark-1024.png` (`Assets/HearApp/Resources/Brand/`) is a flat, full-color gradient
bitmap - the four ellipses are solid filled shapes, not a stroke/outline, so the alpha-erosion
trick that thinned the nav icons (see [001](001-home-screen-visual-parity.md)'s notes) does not
meaningfully help here (tested; the ellipses are hundreds of px across, a 1px erosion is
imperceptible on a solid fill). This needs either a re-exported source asset with smaller/lighter
ellipses, or a decision to redraw the mark's ellipses procedurally in code (so their size becomes
a tunable parameter like everything else in `VisualTokens`) instead of shipping it as one flat
bitmap.

## Definition of done

- [x] Logo mark's ellipses visually match the reference's proportions (side by side comparison).
- [x] Approach decided: new source export vs. procedural redraw.

## Notes

- 2026-09-25: Resolved via `hear-logo-handoff-v3.1` (`sources/hear-logo-handoff-v3.1.zip`),
  which explicitly supersedes v3.0, v2.2 and every earlier logo asset. Decision: new source
  export (not procedural), same as the earlier v2.2 attempt, but v2.2 was reverted by the human
  before shipping - only v3.1 is integrated now. Full package archived at
  `Hear/Art/Reference/brand/v3.1/`; `Assets/HearApp/Resources/Brand/` carries its three PNG
  exports (`hear-logo-with-claim-on-light-1024`, `hear-logo-no-claim-on-dark-1024`,
  `hear-mark-only-on-light-512`), wired in `WorldArt.cs` under the same property names as before
  (`LogoWithClaim`, `LogoOnDark`, `MarkOnly`). v3.1 also formally splits with-claim/no-claim/
  mark-only variants (v2.2 only had one "full" lockup, which is what caused the duplicate-claim
  bug found and fixed in the v2.2 attempt) and drops the claim's trailing period ("Sound opens
  worlds", not "Sound opens worlds.") - the live claim `Label` on Home was updated to match.
  Confirmed on-device (Galaxy Z Fold) against the package's own QA preview board
  (`previews/hear-logo-system-board-v3.1.png`): Home's no-claim logo + separately-drawn claim
  text matches the board's "NO CLAIM / dark theme" panel, slim ellipses, no duplication.
  With-claim (Splash, ~0.8s) and mark-only (nav rail, needs a non-Home screen) are wired
  identically but not separately screenshotted - on-device nav taps became unreliable partway
  through this check (see [002](002-shell-navigation-functional.md), unrelated to this item).
- 2026-09-25: Logged per human instruction to track feedback that can't be implemented in the
  work session it was raised in (`AB-ROADMAP-005`).
