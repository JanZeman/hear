# All shell navigation and flow buttons functional end-to-end

**Status**: Open
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

On the real device, verify (and fix where broken) every interactive control in the shell flow:
bottom nav (Worlds / Results / Settings), world carousel side-peek tap-to-select, Play,
Headphones / Speaker choice, "Got it" micro-instruction continue, "Back to Worlds" on Results.
Each must actually transition `GameFlowController` state and produce a visible, correct screen,
not just render without throwing.

## Definition of done

- [ ] Each control listed above verified on-device with the correct resulting state/screen.
- [ ] No dead/no-op button remains.

## Notes
