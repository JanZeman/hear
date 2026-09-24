# HEAR UI implementation assets v0.3

This package closes a gap in the earlier handoff: the coding agent was given visual boards but not enough *separate usable assets*.

## What is now safe to use directly

- `assets/brand/`: exported HEAR logo/mark. The PNG exports are authoritative for appearance; SVGs are vector construction references.
- `assets/companion/`: transparent Companion sprites extracted from the approved concept board. They are approved for the prototype shell.
- `assets/worlds/`: clean, UI-free prototype crops in 16:9, 4:3, 1:1, and 9:16 plus blurred ambient backgrounds.
- `assets/icons/`: simple shell icons.

## What remains reference-only

Everything in `references/` is there to show composition, hierarchy, mood, and target fidelity. Do not slice UI controls out of those boards.

## Important

Do not rebuild the HEAR logo by eyeballing the mood board. Use the supplied exports. Do not use the full mood boards as home-screen backgrounds when a clean world crop is supplied.
