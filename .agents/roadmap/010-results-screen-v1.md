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
- [x] Full walkthrough on-device of both contexts after the layout fix (Overall extensively;
      post-session via a real Tide Troubles session) - both clean, no overlap.
- [x] `Full hearing profile`'s Left/Right/Both tab interactivity re-verified after the layout fix,
      and unified with the preview section (see Notes) so both are interactive.
- [x] Closer design-fidelity pass against the approved board (see Notes): brand header, info-dot
      affordance, colored stat/utility accents.
- [ ] Per-world "context extra" content is still generic (session detection-rate stats) rather than
      real per-world mechanics (e.g. Tide Troubles' actual fish-caught count) - the engine doesn't
      track those; documented as an honest gap, not faked data.
- [ ] Y-axis loudness labels ("Louder/Normal/Quieter sounds") deliberately not reproduced - this
      engine measures detection rate at one fixed volume, not a threshold, so those labels would
      overstate precision. A standing, deliberate deviation from the approved board, not a gap.

## Notes

- 2026-09-26: Design-fidelity pass after human review ("k finalni verzi jak byla v handoff to ale
  ma stale silene daleko"), closing the gap against `previews/results-scroll-board-approved.png`
  as far as practical without fabricated art or dishonest data:
  - **Brand header**: every context now shows "Results" + a small `WorldArt.LogoOnDark` lockup +
    "Your hearing journey", matching every column of the approved board (previously just the bare
    "Results" title). Hit the exact same flex-shrink-collapses-to-0 bug this item's main fix
    already covers, in a new spot - the header itself lacked `flexShrink:0`, so it got crushed by
    the same ScrollView-viewport mechanism once it became a second multi-child stack. Fixed the
    same way.
  - **Hearing profile preview**: was a stripped-down, non-interactive, axis-less glimpse; the
    approved board's preview and full sections are near-identical (same tabs, same axis). Unified
    both into one `BuildHearingProfileSection` builder - preview is now shorter but equally
    interactive.
  - **Info-dot affordance**: added a small drawn "i" circle (not a Unicode "ⓘ" glyph - text-shaping
    reliability is already a proven risk area in this file) beside titles the approved board marks
    with one (Overall hearing age, This session, Hearing profile, Measurement quality).
  - **Colored accents**: the approved board's per-row colored icons (fish/leaf/target stats,
    lightbulb/trash/refresh utility rows) have no matching art in this codebase - added small
    colored dots in their place for some visual rhythm rather than either inventing icon art or
    leaving those rows plain. Found and fixed a related bug while wiring these in: `Button.text`
    creates its own internal label lazily, which fights `Insert()`'s ordering - a dot inserted at
    index 0 still rendered *after* the button's text. Fixed by not using `Button.text` at all for
    these two rows, building the [dot, Label] children by hand instead (the same pattern the empty
    state's Play button already used successfully).
  - Verified on the S9+: both Overall and a real post-session (Tide Troubles) render cleanly with
    all of the above, no regressions from the layout fix.
  - Unrelated bug noticed, not fixed (out of this item's scope): selecting River Journey and
    tapping Play throws `InvalidOperationException: The URP Lit shader is unavailable` and a
    `MeshCollider` component error, logged to an in-app dev console - the same category of
    build-stripping issue as the earlier Tide Troubles particle-shader bug (roadmap 001), just for
    a different shader/world. Worth its own roadmap item if not already tracked under 003.
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
