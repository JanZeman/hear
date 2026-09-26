# Results screen v1 (Overall + post-session)

**Status**: In Progress
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

Build the Results screen per `sources/Results/hear-results-handoff-v1.0`: one reusable scrollable
page rendered in two contexts - "Overall" (bottom nav's Results tab) and "Post-session" (right
after finishing any world) - matching the approved board's section structure (summary card,
hearing profile preview, measurement quality, then below the fold: full profile, progress trend,
recent sessions, context extras, utility actions), plus a dedicated empty state for zero sessions.

## Definition of done

- [x] Engine records per-frequency trial results (`SessionResult.FrequencyTrials`), not just
      aggregate counts - needed for a real (not invented) hearing-profile chart.
- [x] Local session history persists across app restarts (`SessionHistoryStore`, PlayerPrefs+JSON).
- [x] `HearingAgeEstimator`: transparent, documented-as-approximate hearing-age + measurement
      quality heuristic from real trial data.
- [x] Reusable `ResultsScreenBuilder` renders both contexts from one code path; `ResultsContext` is
      implicit (Overall vs `HasJustCompletedSession`), not a 3-way per-world enum, so any current or
      future world (not just Tide Troubles/River Journey) works without special-casing.
- [x] Empty state (no sessions yet) matches the dedicated reference's structure.
- [x] Wording rules followed: no "Good job"/"Nice job", no age-group comparison (app has no age
      input), "Stable estimate" only once the reliable-session threshold is truly met.
- [x] Critical layout bug (see Notes) found and fixed on a real device with a classic aspect ratio.
- [ ] Full walkthrough on-device of the post-session context specifically (verified Overall
      extensively; post-session shares the same fixed code path but hasn't been re-screenshotted
      since the layout fix).
- [ ] `Full hearing profile`'s Left/Right/Both tab interactivity re-verified after the layout fix.
- [ ] Per-world "context extra" content is still generic (session detection-rate stats) rather than
      real per-world mechanics (e.g. Tide Troubles' actual fish-caught count) - the engine doesn't
      track those; documented as an honest gap, not faked data.

## Notes

- 2026-09-26: Critical bug found and fixed: human reported the Results screen as "silene zmatecna"
  (extremely confusing/unusable) on a newly-connected classic-aspect-ratio phone (Galaxy S9+,
  1080x2220). Screenshots showed every card's text overlapping illegibly. Root-caused via a
  temporary on-device layout dump (logged each element's resolved `layout` rect through logcat,
  not guessed from pixels): UI Toolkit's default `flex-shrink` is `1`, so once the stacked cards'
  natural total height exceeded the ScrollView's viewport height, cards got flex-shrunk to fit
  instead of the ScrollView actually scrolling - non-text wrapper rows (the big-number row, the
  progress bar, tab rows) were crushed to a measured height of literally 0, while plain Labels
  (given `flexShrink:0` in an earlier, now-superseded attempt at this same bug) refused to shrink
  and painted over their now-collapsed siblings. Fix: `flexShrink = 0` on every card
  (`ResultsScreenBuilder.MakeCard`) - once a card refuses to shrink, nothing inside it is ever
  squeezed, and the ScrollView correctly scrolls the overflow instead. Verified via a second layout
  dump (every element's height now correct and non-zero) and a full-page screenshot + scroll-through
  on the S9+: clean, no overlap, matches the approved board's structure.
  Two earlier fix attempts before finding the real cause, kept as history since they explain the
  final code's shape: (1) assumed a text-measurement/ICU problem (the "No ICU data provided"
  logcat warning was a red herring) and added `minHeight` to every `MakeLabel` label - this alone
  did not fix it, but is still correct/harmless defensively; (2) considered a first-frame text-job
  settling race - ruled out by two screenshots two minutes apart showing an identical broken layout.
- 2026-09-26: Also hit substantial on-device testing friction unrelated to this feature's own
  code, logged here only so it isn't re-discovered from scratch: (a) the original Galaxy Z Fold
  repeatedly hard-froze (needing force-stop to recover) mid-session for reasons never root-caused
  (no exception/ANR in logcat) - abandoned chasing it on that device in favor of the S9+; (b) the
  Z Fold's bottom nav taps were intermittently unrecognized via `adb shell input tap` while other
  taps on the same screen worked, cause unknown, not reproduced on the S9+; (c) `DevOverlay`'s
  "Force Complete Session" button (added this session) only marks `TrialEngine.CompleteSession()`
  early - it does not cancel `GameFlowController.EnterWorldRoutine`'s own `RunSession` coroutine,
  so it does not actually shortcut a real session's remaining trials. A real fix (making
  `RunSession` cancellable) was not implemented - toggling the existing "Long session" Settings
  checkbox off before testing is the reliable workaround.
- 2026-09-26: Initial implementation (data layer + builder + empty-state polish) per
  `sources/Results/hear-results-handoff-v1.0`'s dev-agent prompt. Key design calls: (1) hearing
  profile chart plots per-frequency *detection rate*, not a clinical dB threshold (this engine
  presents tones at one fixed level, so a real audiogram threshold isn't something it can honestly
  measure) - shown with a custom `LineChartElement` (Painter2D-drawn, reused for the progress trend
  chart too) and a "reliably detected" shaded band instead of the reference's literal
  "Louder/Quieter sounds" labels, which would overstate the precision of what's measured; (2) the
  Overall Results background is a procedural gradient in the visual bible's own Aurora colors
  (`GradientBackgroundElement`), not a photo asset - none was supplied, and inventing one would
  contradict this project's "no fabricated art" precedent; (3) post-session's "Today's session"
  extra card uses real engine stats (detections/reaction rate/misses) rather than fabricated
  per-world mechanic stats (e.g. a "fish caught" count) the engine has no way to actually measure.
