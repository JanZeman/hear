# Real audiometric-difficulty scoring

**Status**: Idea
**Milestone**: -
**Depends on**: -

## What needs to happen

The Playing HUD now awards variable points per correct detection
(`TrialEngine.PointsForFrequency`), but it only looks at the trial's reference frequency (higher
frequency -> more points) as a placeholder - it has no idea how quiet or otherwise hard the tone
actually was, because `TrialSpec`/`TonePlayer` don't carry per-trial volume/audibility data yet.
Once the real descending-staircase audiometric protocol exists (see the "simplified one-trial-
per-frequency stand-in" note in `Hear/README.md`), replace the frequency-only formula with one
driven by actual trial difficulty - e.g. tones played quieter (closer to a detection threshold)
or off the easy reference set should be worth more, per human direction 2026-09-26.
