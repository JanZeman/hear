# Recommended implementation plan

The coding agent should inspect the existing Unity project first and preserve working architecture where sensible.

## Phase A — Architecture and responsive shell

1. Inspect current project structure, Unity version, render pipeline, existing 2D prototypes and trial engine.
2. Document current architecture before changing it.
3. Introduce the minimum world-presentation boundary needed to decouple measurement from presentation.
4. Implement shell navigation and responsive/adaptive layout modes.
5. Add desktop window resizing support and minimum-window behavior.
6. Implement cold-start randomized selected world while preserving stable carousel order.
7. Implement headphone-recommended / speaker alternative flow without blocking play.

## Phase B — Mock trial driver

Create a development-only way to run deterministic sequences of outcomes without real hearing measurement, so world presentation can be developed/tested independently.

Example sequence can include all four outcome types and Left/Right/Combined channels.

## Phase C — Three prototype worlds

Implement minimal but functioning prototypes:

- Tide Troubles: layered 2D + autonomous success gag.
- Paper Garden: layered 2.5D + parallax + sequenced stage actions.
- River Journey: simple 3D path + canoe + camera + paddle success impulse.

Use primitives/placeholders. Do not spend large effort recreating final art yet.

## Phase D — Integration proof

Run the exact same deterministic trial sequence through all three worlds and verify that measurement/session results are identical.

## Phase E — Art pipeline

Only after the architecture passes acceptance:

- request/generate production-ready layered assets,
- import optimized textures/models,
- replace placeholders incrementally,
- preserve responsive composition and the same world contract.
