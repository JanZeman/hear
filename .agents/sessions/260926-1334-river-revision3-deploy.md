# River of Echoes revision 3 deployment

**Continues**: `260926-1330-river-revision3-device-check.md`

## Active topic

Verify the freshly deployed River of Echoes build on iPhone.

## Queued topics

None. Disclosure work remains out of scope.

## Completed work

- Confirmed the working repository is `/Users/jan/Dev/HR1` (`JanZeman/hear`). The `/Users/jan/Dev/HR`
  location was only the source path supplied for the original handoff ZIP.
- Built the current Unity iOS project, completed the Xcode Debug build/sign, installed it to the
  connected iPhone, and launched it.
- Captured the post-deploy app screen at
  `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/river-visual-pass-revision3-post-deploy.png`.
- The app is at the world selector. The River scene has not yet been opened on this deploy.

## Decisions and constraints

- Preserve the approved single-scene, stylized 3D River scope.
- Do not start another normal 30-second audio session without the user's approval.
- Leave disclosure and sandbox settings untouched.
- Keep work uncommitted; follow `AB-GIT-002` at final handoff.

## Changed files

- `.agents/sessions/260926-1334-river-revision3-deploy.md`
- Earlier River, roadmap, fix-log, and session changes listed in prior snapshots remain in place.

## Verification and open risks

- Unity iOS build succeeded.
- Xcode Debug build/sign succeeded.
- `devicectl` installed and launched `com.janzeman.hear` on the iPhone.
- Screenshot confirms the world selector loads after deployment. The third-build River visuals, paddle
  response, progressive village reveal, villagers, and chief welcome remain unverified on-device.

## Next steps

1. Ask the user to select River of Echoes and tap Play for a normal session.
2. Capture promptly and inspect the sky gradient, water reflections, canoe, and village framing.
3. If the scene reads well, use an approved later test to verify paddle feedback and the final welcome.
