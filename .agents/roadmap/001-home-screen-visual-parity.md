# Home screen 1:1 visual parity with GOLDEN concept board

**Status**: In Progress
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

Bring the running Home / World Selector screen to a visual 1:1 match with
`sources/HEAR-App-UI-Home.png` and `sources/HEAR-App-UI-Concept-Board.png`, verified against the
real connected Android device (Galaxy Z Fold, `SM-F956B`), not just the Editor Game view. Iterate:
build -> install -> screenshot -> compare -> adjust `ShellUIController.cs` / `WorldArt.cs` /
`VisualTokens.cs`.

## Definition of done

- [ ] Every item in `Hear/docs/v1.0-home-handoff/02-developer-checklist.md` verified true
      on-device.
- [ ] Side-by-side screenshot of the running app vs. `sources/HEAR-App-UI-Home.png` shows no
      structural difference (layout, carousel, companion placement, nav, brand).
- [ ] The issues listed in `Hear/docs/v1.0-home-handoff/current-implementation-delta.md` are
      confirmed resolved on-device.

## Notes

- 2026-09-25: Started. Using the connected physical device instead of an emulator for accurate
  density/safe-area (`Hear/README.md` notes screenshots were never captured in a prior session).
