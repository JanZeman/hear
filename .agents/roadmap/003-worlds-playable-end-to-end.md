# All three worlds playable end-to-end on-device

**Status**: Open
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

For each of Tide Troubles (2D), The Paper Garden (2.5D) and River Journey (3D): run a full
session on the connected device (real audio-timed tone/catch trials, not `DevOverlay` mock
injection) and confirm the world reaches Results with a sensible `SessionResult`, matching the
shared `TrialEngine` contract described in `Hear/README.md`.

## Definition of done

- [ ] Tide Troubles: full session completes on-device; ambient creature reactions fire on
      `CorrectDetection`.
- [ ] The Paper Garden: full session completes on-device; staged garden progression advances
      correctly.
- [ ] River Journey: full session completes on-device; canoe progresses along the path and the
      paddle bump/impulse fires on `CorrectDetection`.

## Notes

- 2026-09-26: River Journey blocks on entry on a Galaxy S9+ (Android, real device, not Editor):
  tapping Play throws `InvalidOperationException: The URP Lit shader is unavailable` plus a
  `MeshCollider` "component doesn't exist" error, shown in an in-app dev console; the shell stays
  visually on World Selector underneath. Same build-shader-stripping category already found and
  fixed once for Tide Troubles' particle shader (see roadmap 001's notes on
  `ProjectSettings/GraphicsSettings.asset`'s `m_AlwaysIncludedShaders`) - likely needs the URP Lit
  shader's GUID added there too, plus whatever is trying to add a `MeshCollider` investigated
  separately. Not fixed - found incidentally while verifying the Results screen (roadmap 010) and
  logged here rather than chased, since it's this item's scope, not that one's.
