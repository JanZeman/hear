# Reduce visual weight of the logo mark and re-check other brand/UI art

**Status**: Open
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

- [ ] Logo mark's ellipses visually match the reference's proportions (side by side comparison).
- [ ] Approach decided: new source export vs. procedural redraw.

## Notes

- 2026-09-25: Logged per human instruction to track feedback that can't be implemented in the
  current work session (`AB-ROADMAP-005`). Not started.
