# Viking Boat shield placement handoff

## Active topic

Viking Boat reward shields now attach alternately left and right of the original ship disk,
moving one slot farther from its centre per pair. The owner approved the original disk anchor
before this layout change.

## Queued topics

None. The owner intends to restore left-to-right placement only after the centre-outward layout
has been reviewed.

## Completed work

- Measured original disk centre retained in `VikingBoatPresentation.cs` as ship-local
  `(0.52, -0.82, 0.20)`.
- Activated alternating placement: first left, first right, second left, second right, and so on.
- Kept the sequential row method available behind the placement mode selector. Both modes use
  the same hull profile and camera-height calculation.
- Recorded exact spacing and derived first six Z positions beside the code. The first pair
  skips one slot next to the original disk, following the previously accepted clearance rule.
- Built, installed, and launched the development APK on the connected Galaxy S9+ with
  `./build.sh oid`.

## Decisions and constraints

- Preserve the approved original disk centre and the sequential code for the later stage.
- Do not switch to left-to-right order until the owner reviews the alternating layout.
- Leave the roadmap untouched per the owner's direction.

## Changed files

- `Hear/Assets/HearApp/Worlds/VikingBoat/VikingBoatPresentation.cs`
- `.agents/fixes/_FIXES_CATALOG.md`
- `.agents/fixes/viking-shield-row-alignment.md`
- This snapshot.

## Verification and open risks

- Android build, installation, and launch succeeded.
- In-session capture showed disks on both sides of the original. The right-side disk appears
  slightly proud of the hull near the stern; the owner has not yet reviewed that spacing.
- The first pair was observed on-device. Later pairs follow the same arithmetic but have not
  been individually inspected in a settled frame.

## Next steps

1. Ask the owner to review the alternating positions on the connected Android.
2. Adjust spacing or hull clearance if that review finds a mismatch, keeping the approved centre.
3. Once the centre-outward positions are accepted, switch to the retained sequential method.
