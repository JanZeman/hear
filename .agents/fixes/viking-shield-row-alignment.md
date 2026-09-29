# Anchor Viking Boat reward shields on the original disk

**Reported by**: Internal
**Affected area**: Viking Boat world
**Fixed**: 2026-09-29
**Related**: -

## Symptom

Mounted reward shields did not establish a reliable centre on the original disk built into the
ship. Spreading them into a row made that misalignment difficult to isolate.

## Cause

The earlier row correction adjusted projected height while leaving the reward disks spread along
the hull. A fixed offset from the original disk also shifted the reward disk in the camera view
as the ship moved.

## Fix

Use the measured centre of the original disk as the placement anchor. Move each new reward disk
toward the camera along the ray through that centre, clearing the decal while preserving its
projected position. Replace the previous reward disk at that spot to avoid overlapping faces.
Keep the row-placement calculation in the source for the later layout stages.

The owner confirmed this centre on-device. Reward disks now arrive alternately to the left and
right, one slot farther from the centre per pair. The exact anchor, spacing, protected centre
zone, slot order, and derived positions are recorded together in `VikingBoatPresentation.cs`.

For layout inspection, a temporary preview now creates all twelve reward shields when the world
opens. Player outcomes no longer create or move shields while that preview is enabled. The
alternating reward path remains in the source for use after the layout is corrected.

## Verification

`./build.sh oid` built, installed, and launched the Android development APK on the connected
Galaxy S9+. An in-session device screenshot after several successful catches showed one reward
disk at the original disk's position, without a second disk visible beside it.
The following Android build succeeded and an in-session screenshot showed reward disks on both
sides of the original while the ship passed the camera.
The twelve-shield preview has not reached the device yet: the next `./build.sh oid` failed because
Unity Licensing Client lost its connection, so Package Manager withheld Input System, glTFast,
and URP and the project could not compile.
