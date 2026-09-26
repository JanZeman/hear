# Restore River scene entry on mobile

**Reported by**: Internal
**Affected area**: River Journey runtime scene construction and shader inclusion
**Fixed**: 2026-09-26
**Related**: Roadmap 003, All three worlds playable end-to-end on-device

## Symptom

Entering River of Echoes in mobile development builds failed with an unavailable URP Lit shader
exception and an error while creating a `MeshCollider`.

## Cause

The always-included shader list referenced the wrong GUID for the installed URP Lit shader.
`GameObject.CreatePrimitive` also attempted to add unused colliders, including a MeshCollider
for the river plane, before the scene disabled them.

## Fix

Corrected the URP Lit GUID and now build River props from Unity's built-in meshes on render-only
objects, without creating colliders.

## Verification

Unity's integration proof passed for all worlds. The corrected iOS build installed on the iPhone;
River of Echoes entered and completed a normal 30-second session to Results without the prior
entry failure. The runtime scene now uses only render meshes, with no collider creation path.
