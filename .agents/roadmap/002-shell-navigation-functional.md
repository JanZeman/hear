# All shell navigation and flow buttons functional end-to-end

**Status**: In Progress
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

On the real device, verify (and fix where broken) every interactive control in the shell flow:
bottom nav (Worlds / Results / Settings), world carousel side-peek tap-to-select, Play,
Headphones / Speaker choice, "Got it" micro-instruction continue, "Back to Worlds" on Results.
Each must actually transition `GameFlowController` state and produce a visible, correct screen,
not just render without throwing. Before the first session, require explicit acknowledgement that
HEAR is not a medical device, including through developer shortcuts. Show the reminder on every
Results screen.

## Definition of done

- [ ] Each control listed above verified on-device with the correct resulting state/screen.
- [ ] No dead/no-op button remains.
- [ ] The non-medical notice appears before the first game, requires OK, and is not shown again
      after acknowledgement.
- [ ] A non-medical reminder appears on populated, empty, and early-ended Results screens.

## Notes

- 2026-09-26: Added the approved first-game non-medical notice and Results reminder scope. The
  notice must gate both standard and developer session entry routes.
- 2026-09-26: Implemented the notice gate and reminders on standard, empty, and early-ended
  Results screens. The Unity iOS build, Xcode Debug build with automatic signing, device install,
  and cold launch succeeded. The connected iPhone displayed the new first-play notice. Persistence
  after acknowledgement and Results variants remain unverified on-device.
