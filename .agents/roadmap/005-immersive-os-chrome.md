# Show OS status bar, hide only the OS navigation bar (Android + iOS)

**Status**: Open
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

Corrected understanding (human feedback 2026-09-25, superseding this item's original framing):
this device normally shows both a top status bar and a bottom OS navigation bar; the reference
mockups (`sources/HEAR-App-UI-Home.png`) deliberately keep the status bar's icons visible/lit. So
the target is **not** "hide everything" - only the bottom OS navigation bar should be
immersive-hidden; the top status bar should stay visible, matching the design.

Attempted this session on Android (`GameFlowController.ApplyAndroidStatusBarVisibleNavHidden`,
called from `Awake()` and `OnApplicationFocus`):

- The nav-bar-hide part works and is confirmed correct/intentional: swiping from the bottom edge
  temporarily reveals it (immersive-sticky), then it re-hides - expected Android behavior, not a
  bug (human confirmed after asking).
- The status bar stays hidden despite explicitly clearing `WindowManager.LayoutParams.FLAG_FULLSCREEN`
  and never setting `View.SYSTEM_UI_FLAG_FULLSCREEN`. `adb logcat` (`InsetsController: onStateChanged`)
  shows the status bar's `InsetsSource` flips to `mVisible=false` within ~3ms of Activity start,
  before our code's `runOnUiThread` callback can plausibly run - something in Unity's own native
  startup path hides it independent of our C#.
- Also tried `PlayerSettings.Android.startInFullscreen = false` in `HearDevelopmentBuild.cs` (now
  set for Android builds) - this fixed an unrelated double-hide-then-partial-reapply flicker at
  startup, but the status bar still ends up hidden. `androidFullscreenMode` (still `1` in
  `ProjectSettings.asset`) was never changed and is the next thing to try - unknown what its
  numeric values map to (`AndroidFullscreenMode` enum; not found via `strings` on the local
  `UnityEditor.dll`, would need Unity's own docs/source).
- A real NullReferenceException bug was found and fixed along the way: the original
  `AndroidJavaObject`/`AndroidJavaClass` instances were held in `using` blocks that disposed them
  synchronously, but `Activity.runOnUiThread` only *posts* the runnable - it does not block until
  it runs - so the captured references were already invalid JNI refs by the time the runnable
  executed. Fixed by not disposing `unityPlayer`/`activity` before the runnable runs.
- **iOS**: no iOS build produced or tested this session at all - completely unverified, no
  equivalent of this problem investigated there yet.

## Definition of done

- [ ] Android: status bar visible with its icons legible over the app's background, OS nav bar
      immersive-hidden (revealed temporarily on edge swipe), confirmed on-device.
- [ ] iOS: status bar visible, home indicator auto-hidden (`prefersHomeIndicatorAutoHidden`),
      confirmed on a real device or simulator.

## Notes

- 2026-09-25: Original framing of this item ("hide both bars") was wrong - corrected by the human
  after seeing screenshots; rewritten above. Investigation paused after several build/deploy
  cycles without resolving the status bar specifically, per the human's own instruction to log
  what can't be resolved in-session rather than keep guessing blind (`AB-ROADMAP-005`). The nav-bar
  half of this item is done and verified; only the status-bar-visible half remains open.
