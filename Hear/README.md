# Hear — Game Description

This document describes **what the game is and how it should feel and behave**, for anyone
(human or AI agent) picking up design or implementation work on it.

## 1. One-line pitch

A short, genuinely fun mobile game that produces sounds of varying pitch and loudness; the
player reacts whenever they notice one. Under the hood, this is a hearing screening — but the
player experiences it purely as a game, never as a medical test.

## 2. Why this exists

Existing hearing-test apps are clinically fine but boring (barriers, forms, payment, dry pure-
tone lists). Existing "fun" apps either test the wrong thing (sound *recognition* or *pitch
discrimination*, not *audibility*) or are gated behind a professional access code. The gap this
project fills: a **real, audiologically-valid go/no-go detection test, wrapped in an actual game**
that a 6-year-old and a 60-year-old would both enjoy playing, with zero setup friction.

## 3. Audience and tone

- **All ages**, not children-only. This is a deliberate choice: it avoids child-data privacy
  rules (COPPA/GDPR Art. 8) entirely, and the real-world problem it screens for (age-related
  high-frequency hearing loss) affects a much larger population than pediatric hearing loss.
- The game must never feel like a medical device or a form to fill out. No accounts, no sign-up,
  no payment, no clinical language, ever.

## 4. Playable instantly, no barriers

- Playable instantly. No account, no permissions beyond audio, no age check, no barriers.
- The player just plays one of the game's "worlds" (see Section 6). Under the hood it's running a
  hearing screening; the player never needs to know that.
- Ends at a friendly, non-alarming result screen (see Section 7).
- Nothing beyond audio playback and touch input is ever requested before the result screen — no
  permissions, no forms, no waiting.

## 5. The core mechanic (do not change this without a very good reason)

The single interaction, underneath every visual theme, is:

> **A sound-linked visual cue appears. If the player taps anywhere on the screen while it is
> active, that's a "hit." If they don't, it fades away as a "miss." Sometimes, at random, nothing
> plays at all (a "catch trial") — a tap during one of these is a false positive.**

This is a **go/no-go detection** task — the standard, clinically valid technique used by real
audiologists for young children ("conditioned play audiometry"). It is deliberately **not**:

- **Recognition** ("what animal made that sound?") — tests general knowledge/cognition, not
  hearing.
- **Pitch or tone discrimination** ("which of these two is higher?", "match this pitch") — tests
  musical aptitude, not hearing sensitivity. People with completely normal hearing can fail this
  (a real, documented condition called congenital amusia); it measures a different skill entirely.

**Critical rule for every theme's visual design:** the player's tap does **not** need to land on
the visual cue itself, wherever it appears on screen. A tap anywhere during the active window
counts as a hit. Visual position/movement is purely a "juice"/engagement layer (it looks like
you're aiming or reacting to something specific), never a precision/aim requirement. Turning this
into an aiming or reflex-timing challenge would silently corrupt the hearing measurement — it
would start measuring reaction time and hand-eye coordination instead of hearing.

Missing an active cue or a catch trial is never punished harshly or shown as a "fail" — this is a
game, not an exam.

## 6. The audiometric "protocol" behind the game loop

This is the concrete recipe that makes the game loop above into an actual screening:

- **Frequencies tested:** roughly 1, 2, 4, 8, 12, and 16 kHz. The higher frequencies (8–16 kHz)
  are where the fun "ear age" signal actually lives — high-frequency hearing sensitivity declines
  gradually across the entire lifespan, starting as early as someone's teens/twenties, long
  before it affects everyday speech understanding. The 1–2 kHz anchor exists to catch any
  speech-range issue too.
- **Per frequency:** a simple descending staircase — start at a clearly audible level and step
  down until the player first misses it — rather than a full clinical bracketing procedure. This
  keeps a full session to roughly 2–3 minutes.
- **~10–15 active rounds per session**, with roughly 15–20% additional silent catch trials mixed
  in at random.
- **Stereo per-ear testing** when headphones are detected/likely in use; a single combined test
  when relying on the device's built-in speaker.
- **Output:** the highest frequency the player reliably detected is converted into a friendly
  "ear age" number via a population-norm reference curve — never shown as raw decibels or a
  clinical audiogram by default (see Section 7).
- Headphones are **recommended but never required**. The game must always be playable with zero
  setup, even if that means slightly reduced accuracy on a phone's built-in speaker.
- Ambient-noise gating via the microphone is deliberately **not** part of the design — it would
  require an extra permission prompt, which conflicts with the zero-friction principle.

## 7. The game "worlds"

The detection engine and protocol above are completely shared and identical underneath. What
changes between "worlds" is purely the presentation layer: art style, music/sound design,
narration tone, and the specific animation played on a hit/miss. Building one theme should never
require touching the detection logic. Candidate worlds (not yet finalized which ship first):

1. **Energetic** — fast-paced, physical, impact-driven, "Angry Birds"-style hit feedback: pop
   animation, particle burst, screen shake, combo counter. Bright, saturated, high-energy palette.

2. **Calm / "fireflies by the lake"** — a quiet, atmospheric, nighttime lakeside scene. A firefly
   glows and drifts; catching it feels gentle and magical rather than explosive. Painterly,
   atmospheric visuals, with a real mirrored reflection of the scene in the water. A gentle
   in-fiction narrator voice guides the player.

3. **Third world — not yet finalized.** Candidates: a sonar/deep-space "signal hunter" theme; a
   jungle/wildlife safari theme; a noir-detective theme. The goal is for the worlds together to
   cover a real range of moods/ages, not three variations on the same feeling.

## 8. Presenting the result

- Never lead with a raw number, a decibel value, or an audiogram chart. The primary result is a
  simple, friendly, plain-language summary — an **"ear age"** framing (e.g. "your hearing tested
  like a typical 34-year-old's") is the validated pattern to use, since it's fun, universally
  understood, and non-alarming regardless of the actual result.
- A more detailed chart/breakdown can exist as an optional, secondary "for the curious" layer —
  never the first thing shown.
- The tone of the result screen should stay light and game-like ("nice job!", a shareable score),
  not clinical, even though it's genuinely derived from a real measurement.

## 9. Hard design rules — do not violate these

These aren't style suggestions; each one exists to keep the app as a game/entertainment product
rather than sliding into medical-device territory, or to protect the validity of the underlying
measurement:

1. A tap anywhere on screen during an active cue counts as a hit — never require the tap to land
   on the visual cue itself.
2. The game never collects or stores anything identifying a player, especially not for anyone
   who indicates they're under 18. No accounts, no persistent profiles.
3. Nothing beyond audio playback and touch input is ever requested before the result screen — no
   permissions, no forms, no waiting.
4. Missing a cue, or tapping during a catch trial, is always shown gently (if at all) — this is a
   game people should want to keep playing, not a test they can fail.

## 10. Project setup

- Unity 6 (`6000.6.2f1`), Universal Render Pipeline (URP), created from Unity's official "3D
  URP" project template so no later render-pipeline conversion is ever needed.
- Application identifier: `com.janzeman.hear`.
- This is a personal project, developed independently in free time.

## 11. Current status

- Fresh, empty Unity project. No gameplay has been implemented yet — this is the starting point.

## Development builds

Use the Unity Editor menu `Hear > Build Development` to create deterministic local builds without
choosing an output path manually:

- Android: `Builds/Android/Hear.apk` (ARM64, IL2CPP, minimum Android API 26)
- iOS: `Builds/iOS/` (Xcode project)
- macOS: `Builds/macOS/Hear.app`

The Android and macOS menus also provide an `and Run` variant. Build outputs are ignored by Git.
