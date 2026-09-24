# HEAR — Coding Agent Handoff v0.2

This package freezes the current product, interaction, visual-language, responsive-layout, and three-world prototype decisions for **HEAR**. v0.2 is a superset of v0.1 and adds the production-oriented Visual Bible and machine-readable visual tokens.

**Working name:** HEAR  
**Claim:** **Sound opens worlds.**

## Purpose of this package

The next implementation phase should prove one reusable hearing-game architecture across three radically different presentation worlds:

1. **Tide Troubles** — 2D comic action.
2. **The Paper Garden** — 2.5D layered paper / puppet theatre.
3. **River Journey** — 3D canoe journey.

The worlds are intentionally different. If the same hearing/session contract can drive all three without world-specific logic leaking into the measurement engine, the abstraction is probably healthy.

## Read first

1. `docs/00-decisions.md`
2. `docs/02-hearing-game-contract.md`
3. `docs/14-visual-bible.md`
4. `docs/05-responsive-platform-layout.md`
5. `docs/06-world-framework.md`
6. `docs/11-implementation-plan.md`
7. `docs/12-acceptance-criteria.md`

Then read the three world briefs.

## Important status of visual files

Everything in `assets/reference/` is **concept/reference art, not production-ready game art**. Do not slice arbitrary UI controls or final sprites out of the concept boards. Use them to reproduce mood, composition, hierarchy and behavior with placeholders first.

`assets/brand/hear-lockup-reference.svg` is a vector **geometry reference** for the selected logo direction. Typography and exact optical spacing can still be polished later.

## Source description

Where earlier exploratory notes conflict with the explicit decisions in `docs/`, **the docs in this handoff take precedence**. In particular, worlds must not expose stimulus onset visually.


## v0.2 addition

`docs/14-visual-bible.md` is the authoritative visual implementation baseline for the current prototype phase. `assets/brand/hear-visual-tokens.json` provides provisional implementation tokens. Existing v0.1 files are retained unchanged except for version/readme/manifest metadata.
