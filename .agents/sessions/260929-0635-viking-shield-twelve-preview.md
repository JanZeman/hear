# Viking Boat twelve-shield layout preview

**Continues**: `260929-0105-viking-shield-alternating-placements.md`

## Active topic

Show twelve mounted reward shields at once in Viking Boat so the owner can inspect the complete
row without clicking. The previous centre-outward reward sequence is temporarily inactive.

## Queued topics

After inspecting and correcting the preview, restore the player-driven shield sequence and then
consider the retained left-to-right placement path per the owner's earlier direction.

## Completed work

- Added `ShowShieldLayoutPreview` and `ShieldLayoutPreviewCount` in
  `VikingBoatPresentation.cs`.
- At world creation, twelve distinct reward shields are mounted to the moving ship pivot at the
  existing alternating positions. The original embedded disk remains on the ship.
- While preview is active, player outcomes only resume listening; they never add or move a shield.
- Preserved the original, alternating, and sequential placement code for later use.
- Recorded all twelve ship-local Z positions beside the placement code: -0.14, 0.54, -0.31,
  0.71, -0.48, 0.88, -0.65, 1.05, -0.82, 1.22, -0.99, 1.39. The original disk centre is
  (0.52, -0.82, 0.20); mounted X and Y follow the hull and camera alignment functions.

## Decisions and constraints

- This is a temporary geometry preview. Do not restore click-driven rewards until the owner has
  reviewed the twelve-shield row.
- Leave the roadmap untouched per the owner's direction.

## Changed files

- `Hear/Assets/HearApp/Worlds/VikingBoat/VikingBoatPresentation.cs`
- `.agents/fixes/viking-shield-row-alignment.md`
- This snapshot.

## Verification and open risks

- The source has been reviewed locally, but the preview has not run on Android.
- `./build.sh oid` first failed inside the sandbox when Unity Package Manager could not open its
  local socket. The escalated retry reached Unity compilation but the Licensing Client lost its
  connection. Package Manager then withheld Input System, glTFast, and URP, producing missing-type
  compile errors. No new APK was installed.
- The prior Android build may still be on the connected phone and does not contain this preview.

## Next steps

1. Restore Unity Licensing Client and Package Manager availability without changing project code.
2. Run `./build.sh oid` and confirm installation and launch on the connected Android.
3. Inspect the complete row on the device and correct its placement from the owner's feedback.
