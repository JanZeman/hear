# Make the Home world carousel track the drag live, not just jump on release

**Status**: Idea
**Milestone**: -
**Depends on**: -

## What needs to happen

Human feedback 2026-09-25: swipe works, but nothing moves while dragging - only once the swipe
finishes does the carousel jump to the new world. Doesn't read as professional. Investigated (no
code changed yet): confirmed root cause is architectural, not a small bug.
`ShellUIController.OnCarouselPointerMove` only measures drag distance to decide whether to capture
the pointer (so a plain tap still reaches a card's own `ClickEvent` - see that method's own
comment); it never touches any visual position. All actual movement happens in
`OnCarouselPointerUp`, which - only once total drag distance clears `SwipeThresholdPx` - calls
`_flow.SelectWorld(newIndex)`, which rebuilds the whole world-selector screen
(`ShowWorldSelectorScreen`) and plays `AnimateCarouselEntrance`: a fixed fade + 14px vertical rise,
not a horizontal slide, and not connected to the drag's direction, distance, or speed at all. So
today's carousel is really an index-based "pick a world, fade the new one in" screen wearing a
swipe gesture as its trigger, not a physically dragged carousel.

Two options, not mutually exclusive:

- **A - live 1:1 finger tracking on the existing 3 cards (moderate).** In
  `OnCarouselPointerMove`, once captured, apply a `translate` to `_prevCard`/`_activeCard`/
  `_nextCard` proportional to the drag delta (clamped/rubber-banded past the ends, since only 3
  card slots exist). On release: below threshold, animate back to rest; above threshold, animate
  the slide to completion and only then call `SelectWorld` (which still does its own rebuild for
  the new card's content/labels underneath the now-settled position). Reuses the current
  index/rebuild model entirely - this is the contained, "make what exists feel responsive" fix.
- **B - continuous fractional-index carousel (larger).** Replace the fixed 3-slot model with a
  continuous drag position that can reveal more than one card ahead on a fast swipe, add
  momentum/inertia after release, and unify the drag and the entrance animation into one motion
  system instead of two unrelated ones. Touches `ApplyCardRect`, card slot management, and how/when
  world content loads. Reads as more "professional" (closer to a native OS carousel) but is a
  real rewrite of the carousel's internals, not a tuning pass.

No off-the-shelf carousel control exists in Unity UI Toolkit to swap in instead; `ScrollView`'s
snap-scrolling doesn't natively support the overlapping peek-card "coverflow" look already built
here, so either option is still hand-rolled on top of the current `VisualElement`-based approach.

Recommendation (not yet actioned): start with Option A - it directly fixes the specific complaint
("nothing moves during the drag") with a contained change, and Option B's continuous model could
still be layered on top later if wanted.

## Notes

- 2026-09-25: Human direction: we will eventually try Option B (continuous fractional-index
  carousel with momentum), not Option A - but not now. Keep Status: Idea until picked up.
- 2026-09-25: Investigated only, per explicit human request ("zatim nic nemenej, jen udelej
  pruzkum"). No code changed for this item.
