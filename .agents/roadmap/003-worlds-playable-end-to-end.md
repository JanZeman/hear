# All three worlds playable end-to-end on-device

**Status**: In Progress
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
  separately. Initially left unresolved while verifying the Results screen (roadmap 010); both
  entry failures are fixed and recorded in `.agents/fixes/river-scene-entry-errors.md`.
- 2026-09-26: River of Echoes replaces the placeholder presentation in the existing River Journey
  world. A new iPhone screenshot showed the in-app `The URP Lit shader is unavailable` error.
  Unity's integration proof passed for all three worlds in the Editor; at that point, the device
  failure remained to be verified after correcting the shader GUID in `m_AlwaysIncludedShaders`.
- 2026-09-26: The procedural scene used `GameObject.CreatePrimitive` and immediately disabled its
  colliders. Primitive creation still adds built-in colliders first, including a `MeshCollider`
  for the river plane. Replaced it with built-in meshes on render-only objects. Found that the
  earlier URP Lit entry used the wrong GUID; corrected it to the GUID in the installed URP package.
  Unity Editor integration proof and iOS/Xcode Debug builds passed, and the fixed build is
  installed. A cold-launch screenshot shows the world selector without the previous shader error.
  Device runtime verification followed.
- 2026-09-26: The user opened River of Echoes on the iPhone and a normal 30-second session reached
  Results. The scene rendered without blocking on the prior shader failure. The entry screenshot
  showed poor portrait composition, so the camera was moved farther behind the canoe and aimed
  lower. The revised camera was run on-device; screenshots still showed a flat salmon-colored
  background, a small canoe, and an unclear village. The user approved a visual iteration for
  more readable water and sunset, a larger canoe silhouette, and a stronger right-bank destination.
  That pass addresses excessive distance fog, flat water, and weak canoe/village silhouettes.
- 2026-09-26: The first visual build was installed and entered on the iPhone. It separated the
  blue river from the dusk background, but the canoe hull and village remained too small to read
  clearly in portrait. A second refinement extends the camera clip range for the sky gradient,
  adds moving water texture and broader reflections, lengthens the canoe gunwales, and enlarges
  village tents. The second build was entered on-device; the sky still appeared flat and the
  nearest reflections became oversized blocks. The sky quad was facing away from the camera, so
  its orientation was corrected and reflections were tapered. The third build is installed and
  awaiting on-device scene verification.
