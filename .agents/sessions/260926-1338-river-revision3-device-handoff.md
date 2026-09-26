# River of Echoes revision 3 device handoff

**Continues**: `260926-1334-river-revision3-deploy.md`

## Active topic

The third River of Echoes visual revision is deployed and has completed a normal session. Its
sunset gradient still appears not to render in the scene.

## Queued topics

None. Disclosure work remains out of scope.

## Completed work

- Confirmed the working repository is `/Users/jan/Dev/HR1` (`JanZeman/hear`). `/Users/jan/Dev/HR`
  was only the source location provided for the handoff ZIP.
- Rebuilt the current Unity iOS project, completed the Xcode Debug build and signing, installed the
  app on the connected iPhone, and launched it.
- The user opened River of Echoes in a normal session. The session reached Results.
- Captured:
  - `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/river-visual-pass-revision3-post-deploy.png`
  - `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/river-visual-pass-revision3-post-session.png`
  - `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/river-visual-pass-revision3-post-session-later.png`
  - `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/river-visual-pass-revision3-session-end.png`
- The scene screenshots show a flat dark-blue sky rather than the expected gradient. Read-only
  investigation found `BuildCamera` sets that same solid background color and `BuildSunsetSky`
  rotates its gradient quad by 180 degrees. The likely cause is backface culling, but this is not
  yet verified by a correction.
- Proposed a narrow source fix: remove the quad's 180-degree rotation, retain the gradient design,
  verify it, and record a verified correction in the roadmap and fix log. An alternative skybox or
  double-sided material would be a wider change.
- The user asked to stop and produce this handoff before approving the proposed change. No source
  correction or additional deploy was performed after that request.

## Decisions and constraints

- Preserve the approved single-scene, stylized 3D River scope.
- The user explicitly wants step-by-step approval. The proposed source fix is awaiting approval.
- Do not make another device build/install without separate approval.
- Leave disclosure and sandbox settings untouched.
- Keep work uncommitted; follow `AB-GIT-002` at final handoff.
- The work serves Goal 001, KR1 by improving demo readability; it is not evidence of outside
  interest.

## Changed files

- `.agents/sessions/260926-1338-river-revision3-device-handoff.md`
- Existing uncommitted River presentation, graphics settings, roadmap, fix log, and prior session
  files remain as listed in predecessor snapshots.
- No River source file was edited during this device-check segment.

## Verification and open risks

- Unity iOS build succeeded.
- Xcode Debug build and signing succeeded.
- `devicectl` installed and launched the app on the iPhone.
- The normal River session reached Results.
- The sunset gradient, paddle response, progressive village reveal, villagers, and chief welcome
  have not been fully verified on-device.
- The last device capture shows Results. The scene's flat sky is an observed visual issue; the
  proposed quad-orientation cause remains a hypothesis pending approval and test.

## Next steps

1. If work resumes, ask the user to approve the targeted gradient-quad orientation correction.
2. If approved, change and verify the source; only then record the verified result in roadmap 003
   and the fix log.
3. Ask separately before building and installing another device revision.
