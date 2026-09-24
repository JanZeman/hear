You now have `hear-ui-assets-v0.3.zip`. Treat it as the asset-level correction to the earlier HEAR handoff.

The previous instruction not to cut production UI or final sprites from mood boards remains valid. The difference is that design has now explicitly exported the prototype assets you need, so you no longer need to approximate them.

Before changing UI, read `README.md`, `docs/upgrade-current-home.md`, `docs/asset-usage.md`, and `manifest.json`.

Then update the current desktop home/world-selector implementation toward the supplied HEAR target:

- replace improvised branding with the supplied HEAR logo assets,
- use the clean per-world ratio assets rather than annotated concept boards,
- place the supplied transparent Companion as a separate scene element,
- use active-world ambient art to reduce the flat admin-dashboard feeling,
- apply the existing visual-bible tokens for typography, spacing, radii and motion,
- remove invented profile/personalization UI unless it is backed by a real product requirement,
- keep responsive behavior: wide sidebar, medium icon rail, compact bottom navigation,
- never non-uniformly stretch art.

Do not redesign the brand. Do not generate new artwork. Do not crop additional UI assets from moodboards when an exported asset exists.

First make the wide macOS/Windows world-selector visually match the supplied direction, then verify square and narrow portrait window resizing.
