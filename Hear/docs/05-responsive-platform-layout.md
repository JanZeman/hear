# Responsive / adaptive layout

HEAR must support arbitrary practical rectangles: tall phone, square foldable, landscape phone/tablet, and resizable macOS/Windows windows.

## Never stretch artwork

World artwork must not be independently stretched in X or Y to fill the viewport.

Allowed techniques:

- proportional scaling,
- crop/cover,
- reveal more world,
- camera reframing,
- repositioning,
- responsive reflow,
- swapping layout variants at breakpoints.

## Core Safe Square

All worlds must be designed around a central **1:1 Core Safe Square**.

Everything required to understand and play the world must remain viable inside that square.

Extra viewport area is used as atmospheric extension:

```text
landscape: [extra][ CORE 1:1 ][extra]
portrait:  extra above / CORE 1:1 / extra below
square:    CORE dominates
```

This is a composition rule, not a required bitmap resolution.

## Asset bleed

Generated background artwork must contain generous extension/bleed beyond the core composition. Critical subjects must not be placed at the edges of the source asset.

Prefer layered assets to one giant flattened bitmap whenever motion, parallax or responsive composition matters.

## Shell breakpoints

Exact values should be tuned in the implementation, but use three conceptual modes:

- **Wide:** desktop sidebar with text + icon.
- **Medium:** narrow icon rail.
- **Compact:** bottom navigation, even on desktop when the window becomes narrow/portrait-like.

Use logical UI units, not raw physical pixels.

## Desktop resizing

macOS and Windows windows must be continuously resizable down to a defined minimum practical size.

Acceptance behavior when dragging from wide landscape to narrow portrait:

- no distorted art,
- no control overlap,
- navigation adapts,
- core gameplay remains visible,
- world camera/layout reframes rather than stretches,
- secondary details may hide when necessary.

## World-specific behavior

### 2D

Use layered 2D assets with anchor zones and extension regions.

### 2.5D

Use separated transparent layers at different Z positions; reveal more/less and use subtle parallax.

### 3D

Use camera framing / FOV / composition rules. Wide view reveals more world; narrow view protects core subject composition.
