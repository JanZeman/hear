# River of Echoes disclosure handoff

**Active topic**: Add a first-play non-medical disclosure and Results reminder to HEAR.

## Completed work

- Added a `NonMedicalNotice` shell state before any regular or developer session entry.
- The notice requires an OK action and persists acceptance in `PlayerPrefs`; the session-entry
  continuation resumes afterward.
- Added the approved reminder to populated, empty, and early-ended Results screens.
- Expanded roadmap item 002, `Shell navigation functional end-to-end`, with this scope and its
  verification requirements.
- Unity iOS player build, Xcode Debug build with automatic signing, install, and cold launch
  succeeded. The connected iPhone displayed the new first-play notice.
- Markdown lint and `git diff --check` passed.

## Decisions and constraints

- Approved notice: "HEAR is not a medical device and cannot diagnose hearing conditions. It is an
  experimental game prototype."
- Approved Results reminder: "HEAR is not a medical device."
- Acceptance should persist until app reinstall. No disclosure was shown and no session was
  started during development.
- Keep work uncommitted; follow `AB-GIT-002` for the human handoff.

## Changed files for this topic

- `Hear/Assets/HearApp/Core/Shell/GameFlowController.cs`
- `Hear/Assets/HearApp/Core/Shell/UI/ShellUIController.cs`
- `Hear/Assets/HearApp/Core/Shell/UI/ResultsScreenBuilder.cs`
- `.agents/roadmap/002-shell-navigation-functional.md`

## Verification and open risks

- Unity iOS development-player build passed with existing warnings and no C# compile errors.
- The earlier Xcode Release attempt failed because no provisioning profile was selected. This was
  the wrong path: the existing handoff documents Debug with `-allowProvisioningUpdates`,
  `CODE_SIGN_STYLE=Automatic`, and `DEVELOPMENT_TEAM=9VBQGD32YX`. That documented method built,
  installed, and launched successfully using the local signing setup.
- Screenshot: `/Users/jan/.copilot/session-state/2e46fc49-3332-4c6d-bfcf-b3209a6b3299/files/river-of-echoes/disclosure-build-selector.png`.
- The page is visually verified on first launch. Persistence after tapping OK and Results variants
  remain unverified.
- `dotnet` is unavailable in the environment; Unity's player build compiled the project scripts.
- Roadmap item 002 remains In Progress. Other previously existing River world changes in the
  worktree are not part of the disclosure implementation.

## Next steps

1. Resolve Xcode provisioning through the operator's existing development signing setup; rebuild
   and install to the connected iPhone only after confirming the profile is available.
2. Verify notice appearance before first play, OK persistence across app relaunch, and every
   Results variant.
3. Return to roadmap item 003, `All three worlds playable end-to-end on-device`, for River runtime
   verification, including the unresolved MeshCollider report.

## Queued topics

- Continue on-device validation of River of Echoes after the disclosure flow is verified.
