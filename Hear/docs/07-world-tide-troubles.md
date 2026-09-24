# World 1 — Tide Troubles

## Technology target

**2D**.

## Mood

Fast, comic, absurd, energetic. A colorful seaside harbor. Inspired by classic shooting-gallery energy, but not by copying any specific game characters, UI, sound, layout or branding.

## Player fantasy

The harbor is full of ridiculous moving creatures and situations. The player listens. Correct detections cause satisfying autonomous comic action.

## Important mechanic

The player does **not** aim precisely.

On `CorrectDetection`:

```text
tap accepted -> launch net / comic capture device -> auto-select valid target -> gag animation
```

A valid hearing response should produce a successful world event regardless of tap location.

## Ambient examples

- fish jumping,
- gulls crossing,
- crab on a balloon,
- pufferfish,
- moving water,
- dock activity.

Ambient timing must not correlate with tone onset.

## Ear-aware presentation

Optional after classification:

- Left -> select/gag target preferentially on left.
- Right -> select/gag target preferentially on right.
- Combined -> free selection.

## Session progression

The harbor can evolve with normalized session progress, independent of correctness: time of day, boats moving, background activity, or a simple sequence of set pieces.

Correct detections add extra comic payoff but are not required to finish.

## Prototype assets

Use placeholders first. Reference mood: `assets/reference/worlds/tide-troubles-style-frame.png`.
