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
