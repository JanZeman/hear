Use `hear-logo-handoff-v3.1` as the ONLY canonical HEAR logo source.
Ignore and remove older HEAR logo packages/assets where they conflict with this handoff.

Apply the logo as follows:

1. Splash / onboarding branding
- Use `assets/svg/with-claim/...`
- Claim is already part of the lockup.
- Exact claim: `Sound opens worlds`
- No trailing period.

2. Home screen
- Use `assets/svg/no-claim/...`
- Do NOT use the with-claim logo on Home.
- Home renders `Sound opens worlds` separately as live UI text.

3. Compact placements
- Use `assets/svg/mark-only/...`

4. Theme selection
- Use `on-light` on light surfaces.
- Use `on-dark` on dark / cinematic surfaces.
- Geometry must never be modified between themes.

5. Do not modify
- Do not redraw the logo.
- Do not change ellipse widths/heights.
- Do not change horizontal alignment.
- Do not change the common bottom baseline.
- Do not change the gap between mark and HEAR.
- Do not add a period to the claim.
- Do not crop the mark-only asset.
- Do not add guide lines or helper geometry.

6. Prefer SVG
- Use SVG masters wherever the Unity pipeline supports them.
- Use PNG exports only where raster assets are required.

7. QA after integration
Compare runtime output against:
- `previews/hear-logo-system-board-v3.1.png`
- the individual `*-actual-export.png` previews

If runtime appearance differs, fix the integration/layout.
Do not edit the logo source to compensate.

This v3.1 package supersedes v3.0, v2.x, and all earlier HEAR logo assets.
