# Developer implementation checklist

Before coding:
1. Open `references/00-GOLDEN-home-concept-board.png`.
2. Read `README.md`.
3. Read `agent-prompt-home-screen-v1.0.md`.
4. Read `metadata/home-layout.json` and `metadata/companion.json`.
5. Inspect all Home assets.

Then verify your Home screen against this checklist:
- [ ] Uses the locked HEAR logo asset.
- [ ] Logo elements are not reconstructed or respaced.
- [ ] Claim is live text and readable.
- [ ] Uses sharp active-world background.
- [ ] No `FEATURED WORLD` label.
- [ ] No separate thumbnail row.
- [ ] One central selected card with side peeks.
- [ ] World names are not truncated.
- [ ] Companion has no feet.
- [ ] Companion is a separate sprite.
- [ ] Separate shadow used if grounded.
- [ ] Bottom navigation is overlay-style, not a slab.
- [ ] No profile/avatar/greeting filler.
- [ ] Android and iOS differ only in safe areas.
- [ ] Narrow desktop collapses to compact layout.
- [ ] No non-uniform image stretching.

Deliver screenshots for:
- Android portrait
- iOS portrait
- Fold / square
- tablet landscape
- wide desktop
- narrow desktop
