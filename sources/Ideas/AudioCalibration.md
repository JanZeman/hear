# Audio Output Control and Calibration

## Purpose

HEAR depends on sound presentation.

That sounds obvious, but it has a major consequence for the credibility of the product:

> A hearing result is only meaningful if HEAR has reasonable control over the sound level and audio path used during the measurement.

If the same user performs the same session once at a low phone volume and once at maximum volume, the second session may appear to produce a better hearing result even though the user's hearing has not changed at all.

For that reason, output-volume control and calibration must be treated as part of the **measurement protocol**, not as a minor UI preference.

This document defines the product and architectural direction for handling:

- system volume,
- HEAR stimulus amplitude,
- speakers vs headphones,
- device variability,
- volume changes during a session,
- calibration quality,
- measurement quality,
- aggregation of repeated sessions,
- and the stronger calibration opportunities available in a future specialized Tier 2 application.

The goal is not to claim clinical precision from consumer hardware.

The goal is to make HEAR's screening results **controlled, repeatable, explainable, and honest about their limitations**.

---

## 1. The Core Problem

HEAR can generate a digital tone at a known digital amplitude.

That does **not** mean the acoustic level reaching the user's ear is known.

The actual sound level depends on many factors:

- system output volume,
- HEAR's own digital stimulus amplitude,
- phone model,
- built-in speaker characteristics,
- headphone model,
- Bluetooth device behavior,
- hardware gain,
- frequency response,
- operating system audio processing,
- fit and seal of earbuds,
- left/right output differences,
- environmental noise.

Conceptually:

```text
System output volume
        ×
HEAR stimulus amplitude
        ×
Device / transducer behavior
        =
Acoustic output reaching the ear
```

HEAR directly controls only part of that chain.

Therefore:

> HEAR must never treat a digital amplitude value alone as if it were an absolute hearing threshold.

---

## 2. Why System Volume Matters

Consider two otherwise identical test sessions.

### Session A

```text
System volume: 30%
Stimulus amplitude: -30 dBFS
```

### Session B

```text
System volume: 90%
Stimulus amplitude: -30 dBFS
```

The digital test tone is identical.

The acoustic output is not.

The user is much more likely to hear the stimulus in Session B.

If HEAR ignored the system volume difference, it might incorrectly conclude that the user's hearing improved.

The same problem applies to the frequency profile.

A louder overall output can move apparent thresholds across many frequencies.

This can affect:

- hearing-age estimation,
- frequency-based hearing profile,
- left/right comparison,
- session-to-session trend,
- long-term aggregate result.

Therefore, system volume must be considered a first-class input to the measurement.

---

## 3. Fundamental Design Rule

During an active hearing-measurement session:

> **System output volume should remain fixed. HEAR should vary the stimulus amplitude.**

This separation is important.

### System output volume

This should establish the session's output reference point.

Example:

```text
System volume: 55%
```

Once the hearing portion begins, that value should remain constant.

### HEAR stimulus amplitude

This is what the measurement logic changes dynamically.

Example:

```text
-18 dBFS
-24 dBFS
-30 dBFS
-36 dBFS
...
```

The hearing protocol searches for a threshold by changing the stimulus, not by asking the user to repeatedly change the operating system volume.

---

## 4. Pre-Session Audio Gate

Before the hearing measurement starts, HEAR should verify that the listening setup is suitable.

This should feel lightweight and integrated into the experience.

It should not look like laboratory calibration.

A conceptual UI could say:

> **Set your listening level**  
> Adjust the volume until it reaches the marked range. Keep it there during the game.

A simple visual control might resemble:

```text
Too quiet          Ready          Too loud
──────●─────────────○──────────────
```

The exact visual design can match each world.

For example:

- River Journey could use a calm water-level metaphor,
- Tide Troubles could use a playful gauge,
- Paper Garden could use a minimal ink-like indicator.

The underlying rule remains identical.

---

## 5. Why HEAR Should Not Require Maximum Volume

An apparently simple solution would be:

> Ask the user to set the device to 100% volume, then control everything inside HEAR.

This is not a good general strategy.

Reasons include:

- comfort,
- hearing safety,
- unexpected loud transients,
- large hardware differences,
- clipping or distortion,
- different Bluetooth gain behavior,
- accessibility concerns,
- users with sound sensitivity,
- unpredictable device-specific maximum output.

HEAR should instead define a **safe reference range**.

The exact target range will require platform and device testing.

The important product rule is:

> The user should enter a controlled, safe output region before the measurement begins.

---

## 6. Volume Stability During a Session

HEAR should monitor system volume while a measurement is active, where the platform provides sufficient access.

If the user changes volume during the hearing portion:

```text
55% -> 80%
```

the threshold measurements before and after that change are no longer directly comparable.

HEAR should react immediately.

A suitable message could be:

> **Volume changed**  
> Return to the marked level to continue.

Possible implementation policies:

### Minor accidental change

If the user immediately restores the expected level:

- pause measurement,
- restore the protocol state,
- repeat the affected stimulus,
- continue.

### Large or persistent change

If the volume remains outside the allowed region:

- pause the session,
- require correction,
- invalidate measurements collected after the change.

### Repeated changes

If the user repeatedly changes volume:

- mark measurement quality as limited,
- possibly recommend repeating the session.

The system should prefer correctness over silently continuing.

---

## 7. Audio Route Must Be Known

HEAR should know which output path is active whenever possible.

Relevant categories include:

```text
Built-in speaker
Wired headphones
Bluetooth headphones
Bluetooth earbuds
USB audio
Hearing aid / specialized audio route
Other external output
```

The route matters because every category has different calibration characteristics.

A session performed through a phone speaker is not equivalent to a session performed through headphones.

The result model should preserve this information.

Conceptually:

```text
SessionAudioContext
    outputRoute
    deviceIdentity
    systemVolume
    volumeStable
    calibrationProfile
    channelConfiguration
```

---

## 8. Speaker Mode vs Headphone Mode

HEAR should support both, but they should not be treated as equivalent.

### Headphone mode

Advantages:

- more direct sound delivery,
- better isolation from the room,
- meaningful left/right testing,
- potentially more repeatable,
- potentially calibratable for known hardware.

Challenges:

- huge variation between headphone models,
- different fit,
- Bluetooth processing,
- device-specific EQ,
- left/right hardware variation.

### Phone speaker mode

Advantages:

- zero setup friction,
- available to almost every user,
- excellent for casual first contact with HEAR.

Challenges:

- no reliable left/right isolation,
- room acoustics matter,
- distance to phone matters,
- orientation matters,
- environmental noise matters,
- device speakers vary greatly.

Therefore:

> Speaker sessions can still be useful, but their measurement confidence should generally be lower than well-controlled headphone sessions.

---

## 9. Mono Must Still Work

HEAR should not require stereo headphones just to function.

The product should remain accessible through the phone speaker.

However, the result model must understand the limitation.

For example:

### Speaker session

Can estimate:

- overall audibility,
- frequency-dependent response,
- rough hearing-age estimate.

Cannot reliably determine:

- independent left-ear profile,
- independent right-ear profile.

### Headphone session

Can potentially support:

- left ear,
- right ear,
- both,
- inter-ear comparison.

The UI should not fabricate left/right information where the session did not measure it.

---

## 10. Device Variability

A system volume value such as:

```text
50%
```

does not define a universal acoustic level.

Examples:

```text
iPhone + AirPods at 50%
```

and

```text
Android phone + inexpensive Bluetooth headphones at 50%
```

may differ substantially.

They may also differ **by frequency**.

This is especially important for a hearing profile.

A headset with reduced high-frequency output could make a user appear to have worse high-frequency hearing than they actually do.

Therefore:

> Frequency-response differences are potentially as important as overall volume differences.

---

## 11. Calibration Profiles

HEAR should support the concept of a calibration profile.

A calibration profile describes what HEAR knows about the output system.

Conceptually:

```text
CalibrationProfile
    deviceModel
    outputDeviceModel
    routeType
    frequencyResponseCorrection
    referenceVolumeRange
    calibrationVersion
    confidence
```

Not every session will have one.

That is acceptable.

The presence or absence of a calibration profile should influence measurement quality.

---

## 12. Calibration Quality

HEAR should distinguish between the quality of the **audio calibration** and the user's actual hearing result.

A useful internal classification could be:

### Calibrated

The output chain is known well enough to apply a validated calibration profile.

Examples may eventually include:

- tested phone/headphone combinations,
- supported first-party devices,
- known specialized hardware,
- controlled Tier 2 hearing-aid output paths.

### Controlled

The exact acoustic output is not fully calibrated, but:

- system volume is known,
- volume stayed fixed,
- route is known,
- session conditions are acceptable.

This may be sufficient for useful HEAR tracking and approximate screening.

### Approximate

The output path is poorly characterized.

Examples:

- unknown external speaker,
- uncontrolled volume,
- unusual route,
- noisy environment,
- unknown hardware behavior.

The result may still be useful, but should carry lower confidence.

---

## 13. Measurement Quality Is Not Hearing Quality

This distinction must appear throughout the product.

Good:

> **Good measurement quality**

This means:

- the test was technically consistent,
- the data is usable.

It does **not** mean:

> the user has good hearing.

Likewise:

> **Repeat recommended**

means the measurement conditions were insufficient.

It does **not** mean the user's hearing result was bad.

This distinction should be treated as a permanent language rule.

---

## 14. Measurement Quality Model

A future internal model could combine several signals.

For example:

```text
MeasurementQuality
    audioCalibrationQuality
    outputRouteConfidence
    volumeStability
    environmentalNoise
    interactionConsistency
    completionQuality
    leftRightCoverage
```

The user does not need to see every technical field.

The UI may compress this into language such as:

```text
Reliable measurement
Limited measurement
Repeat recommended
```

The detailed signals remain useful internally for:

- result aggregation,
- debugging,
- telemetry,
- future protocol improvements.

---

## 15. Aggregating Results Across Sessions

The long-term hearing estimate should not treat every session equally.

A session performed under well-controlled conditions should generally carry more weight than a poorly controlled session.

Conceptually:

```text
Session A
Headphones
Known route
Stable volume
Low ambient noise
Reliable interaction
=> high weight
```

versus:

```text
Session B
Phone speaker
Unknown positioning
Volume changed
High environmental noise
=> low weight or excluded
```

The exact aggregation formula remains future work.

But the product principle should be established now:

> **Only sufficiently reliable sessions should meaningfully influence the user's stable hearing baseline.**

---

## 16. "Stable Estimate" Must Include Measurement Quality

The phrase:

> Stable estimate

should not mean only:

> We have many sessions.

It should mean something closer to:

> We have enough mutually consistent and technically reliable sessions.

For example:

```text
10 total sessions
6 reliable headphone sessions
2 limited speaker sessions
2 interrupted sessions
```

The aggregate may primarily use the 6 reliable sessions.

The UI could simply show:

> **Stable estimate**  
> Based on 6 reliable sessions

The user does not need to understand the entire weighting system.

---

## 17. Hearing Age Must Not Float Free of Measurement Context

A value such as:

> Hearing age: 39

looks precise.

That precision can be misleading if the session conditions are poor.

Internally, HEAR should always know the context that produced the number.

For example:

```text
Hearing age: 39
Source:
    6 reliable sessions
    headphone-based
    stable output level
    mixed calibration quality
```

The main UI can remain simple, but the system must retain the underlying quality information.

This allows HEAR to choose appropriately between:

```text
First estimate
Early estimate
Building your baseline
Stable estimate
```

---

## 18. Ambient Noise

Output volume is only one side of the equation.

Environmental noise can mask test sounds.

A future protocol should therefore consider ambient-noise detection where technically possible.

Examples of problematic conditions:

- train,
- office conversation,
- television,
- street traffic,
- wind,
- loud household appliances.

A lightweight pre-check might say:

> **Find a quiet place**  
> Background sound can affect your result.

HEAR should avoid becoming fussy.

The objective is not laboratory silence.

The objective is to prevent obviously poor measurement conditions.

---

## 19. Headphone Recommendation

Headphones should generally be recommended for the best HEAR experience.

Possible wording:

> **Headphones recommended**  
> They help HEAR measure each ear separately and reduce interference from the room.

Actions:

```text
Use headphones
Continue with speaker
```

HEAR should not block the user unnecessarily.

Speaker mode remains valuable for accessibility and low-friction discovery.

---

## 20. Known Headphone Profiles

A future HEAR release could maintain calibration profiles for common hardware combinations.

Possible candidates:

- first-party earbuds,
- popular headphone models,
- company-supported devices,
- internal reference devices.

This creates a spectrum of result quality:

```text
Unknown speaker
    ↓
Known phone speaker
    ↓
Unknown headphones
    ↓
Known headphones
    ↓
Validated device + transducer profile
```

HEAR does not need comprehensive hardware coverage before launch.

The architecture should simply allow calibration quality to improve over time.

---

## 21. Platform Constraints

iOS, Android, Windows, and macOS may expose different levels of control over:

- system volume,
- current output route,
- device identity,
- Bluetooth information,
- volume-change notifications,
- audio processing.

HEAR should therefore define a platform-independent abstraction.

Conceptually:

```text
IAudioMeasurementEnvironment
    GetOutputRoute()
    GetSystemVolume()
    ObserveVolumeChanges()
    GetConnectedAudioDevice()
    GetCalibrationProfile()
    GetChannelCapabilities()
```

Platform adapters can implement what is actually available.

The measurement protocol should degrade gracefully when specific information is unavailable.

---

## 22. HEAR Stimulus Engine

The stimulus engine should own the digital part of the measurement.

Responsibilities may include:

- tone generation,
- frequency selection,
- digital amplitude control,
- fade-in/fade-out,
- channel routing,
- left/right selection,
- timing,
- randomized presentation,
- stimulus metadata recording.

A measurement event should preserve enough information for later analysis.

Example:

```text
StimulusEvent
    frequencyHz
    amplitudeDbfs
    channel
    systemVolume
    outputRoute
    calibrationProfileId
    timestamp
    userResponse
```

This ensures that future scoring changes can interpret historical sessions correctly.

---

## 23. Preventing Accidental Loud Stimuli

The audio engine should enforce hard safety limits.

A bug in game logic should not be able to suddenly generate an uncontrolled full-scale tone.

The stimulus layer should therefore have its own constraints.

Conceptually:

```text
Requested amplitude
        ↓
Protocol limit
        ↓
Safety limiter
        ↓
Audio output
```

Safety should not depend only on UI code.

---

## 24. Game Audio vs Measurement Audio

This is particularly important for HEAR because it is a game.

World ambience may contain:

- water,
- birds,
- wind,
- music,
- boat sounds,
- harbor sounds,
- character effects.

Those sounds must not interfere with measurement stimuli.

The audio system should explicitly distinguish:

```text
World audio
UI audio
Measurement stimulus
```

During critical hearing measurements, HEAR may need to:

- duck ambient audio,
- temporarily silence some frequencies,
- pause music,
- create a short listening window,
- resume the world immediately afterward.

This can still feel completely natural to the player.

The game design should support the measurement rather than compete with it.

---

## 25. Example: Tide Troubles

A Tide Troubles sequence might work as follows.

### Normal gameplay

Harbor ambience is active.

### Listening moment

The world visually signals attention.

Ambient sound gently reduces.

HEAR presents a controlled stimulus.

### User response

The player reacts through the normal game mechanic.

### Return to gameplay

Harbor ambience smoothly returns.

The player experiences a game event.

The measurement engine records:

```text
frequency
amplitude
channel
system volume
audio route
calibration quality
reaction
```

The technical protocol remains invisible.

---

## 26. Example: River Journey

River Journey can use the same measurement engine with a completely different emotional experience.

During a listening moment:

- water becomes quieter,
- visual movement slows,
- a distant sound appears,
- the player responds,
- the world resumes.

The same audio-control rules apply.

This separation is important:

> Worlds change the experience. They do not change the measurement fundamentals.

---

## 27. Changing Output Device Mid-Session

A user may:

- remove Bluetooth earbuds,
- connect AirPods,
- disconnect headphones,
- switch to phone speaker,
- route audio to another device.

That should be treated as a session boundary.

The acoustic reference changed.

Recommended behavior:

> **Audio device changed**  
> We need to check the listening level again before continuing.

Then:

1. pause,
2. re-detect route,
3. re-run the audio gate,
4. establish the new calibration context,
5. restart or repeat affected measurement steps.

Depending on the scoring method, the safest approach may be to begin a new measurement segment.

---

## 28. Tier 2 Has a Major Calibration Advantage

The public HEAR application must work across a huge number of devices.

That inherently limits acoustic certainty.

A specialized Tier 2 application can be different.

If the application controls or knows:

- the hearing-aid model,
- receiver type,
- fitting state,
- device gain,
- audio transport,
- output transducer,
- firmware,
- device-specific frequency response,

then the acoustic chain becomes much more predictable.

Conceptually:

```text
Public HEAR
Phone × OS × unknown headphones × unknown fit
```

versus:

```text
Tier 2
Known application
    ↓
Known device protocol
    ↓
Known hearing aid
    ↓
Known receiver
    ↓
Known output characteristics
```

This can make a Unity-based self-fitting experience technically much more controlled than a generic public screening session.

---

## 29. Why This Matters for Self-Fitting

A future Tier 2 self-fitting flow may not merely ask:

> Can you hear the sound?

It may also ask users to make preference or clarity judgments.

Examples:

```text
A or B?
Too sharp / comfortable?
Speech clearer here?
Can you hear the distant sound?
Which version feels more natural?
```

The game world can present these choices naturally.

Underneath, the application may adjust hearing-aid parameters.

The stronger the output calibration and device knowledge, the more meaningful those interactions become.

Therefore, audio calibration is not only a screening concern.

It is foundational to the larger HEAR / Tier 2 vision.

---

## 30. Product UX Should Hide Complexity

The internal model may become sophisticated.

The user experience should remain simple.

The player should not need to understand:

- dBFS,
- SPL,
- transducer calibration,
- Bluetooth gain,
- frequency-response correction.

The user should see:

> Headphones recommended

> Set the volume here

> Ready

> Volume changed — return to the marked level

> Reliable measurement

That is enough.

Complexity belongs in the protocol and architecture, not in the interaction.

---

## 31. Recommended Result Language

Useful phrases:

> Reliable measurement

> Good measurement quality

> Volume stayed stable

> Headphones used

> Speaker-based estimate

> First estimate

> Building your baseline

> Stable estimate

> Repeat recommended

Avoid technical overstatement such as:

> Calibrated audiogram

unless the implementation can genuinely justify that term.

Avoid implying diagnostic certainty from uncontrolled consumer hardware.

---

## 32. Potential Result Metadata

HEAR may eventually expose optional technical details under something like:

> Measurement details

Example:

```text
Listening method: Headphones
Audio device: Known
Volume: Stable
Environment: Quiet
Measurement quality: Reliable
```

This is useful for technically curious users without burdening everyone else.

---

## 33. Session Compatibility

Not all sessions may be directly comparable.

For example:

```text
Session 1:
known headphones

Session 2:
phone speaker

Session 3:
different Bluetooth earbuds
```

HEAR should preserve this distinction.

The aggregation layer may decide:

- all are usable,
- some are weighted less,
- some belong to separate baselines,
- some should not influence a stable estimate.

This is preferable to pretending that all sessions have identical measurement conditions.

---

## 34. Calibration Versioning

Calibration data will evolve.

Therefore a session should store the calibration version used at the time.

Example:

```text
calibrationProfileId: airpods-pro-2
calibrationVersion: 3
```

If HEAR later improves its calibration model, historical results remain interpretable.

The same principle applies to:

- scoring algorithm version,
- protocol version,
- hearing-age model version.

---

## 35. Protocol Versioning

Every session should record the measurement protocol version.

Conceptually:

```text
Session
    protocolVersion
    scoringVersion
    calibrationVersion
```

This prevents future software changes from silently mixing incompatible results.

A major protocol change may require HEAR to start a new baseline.

---

## 36. Safety and Comfort

Because HEAR intentionally explores hearing thresholds, the stimulus system must be conservative.

Important principles:

- avoid unnecessary maximum output,
- limit stimulus duration,
- use smooth ramps where appropriate,
- prevent accidental full-scale output,
- respect platform safe-listening behavior,
- never encourage users to increase volume beyond comfortable levels,
- stop or reduce output if the user indicates discomfort.

The product should optimize for **reliable perception**, not maximum loudness.

---

## 37. What HEAR Can Claim

With controlled but not clinically calibrated consumer hardware, HEAR can reasonably aim to provide:

- an indicative screening result,
- a repeatable personal baseline,
- frequency-based hearing profile,
- trends under comparable conditions,
- a signal that additional follow-up may be useful.

HEAR should not imply that a consumer-device result automatically equals:

- a professional audiogram,
- a diagnosis,
- a clinical threshold measurement,
- a medical assessment.

Those distinctions protect both trust and product clarity.

---

## 38. Initial Implementation Strategy

HEAR does not need perfect calibration infrastructure on day one.

A sensible progression could be:

### Phase 1

- detect speaker vs headphones,
- recommend headphones,
- require a target system-volume range,
- monitor volume changes,
- invalidate or repeat affected stimuli,
- store output route and system volume,
- distinguish mono vs stereo capability,
- include measurement quality in session data.

### Phase 2

- ambient-noise assessment,
- better device identification,
- calibration profiles for selected common hardware,
- frequency-response compensation,
- more sophisticated session weighting.

### Phase 3

- validated device combinations,
- stronger calibrated mode,
- specialized Tier 2 integration,
- known hearing-aid output chains,
- self-fitting workflows.

This keeps the architecture honest while allowing rapid prototyping.

---

## 39. Proposed Core Domain Types

The exact code design remains implementation-specific, but the domain may eventually include concepts similar to:

```text
AudioOutputRoute
AudioDeviceInfo
AudioCalibrationProfile
AudioMeasurementEnvironment
StimulusEvent
MeasurementQuality
SessionAudioContext
HearingSession
```

An illustrative model:

```text
HearingSession
    protocolVersion
    outputContext
        route
        device
        systemVolume
        volumeStable
        calibrationQuality
        calibrationProfile
    measurementQuality
    stimuli[]
    sessionResult
```

This makes audio conditions part of the result, rather than transient UI state.

---

## 40. Important Design Invariant

The following invariant should be considered foundational:

> A hearing result must never be stored without the audio context that produced it.

At minimum, HEAR should know:

- output route,
- system-volume state,
- whether volume remained stable,
- channel capability,
- calibration quality.

Without this metadata, later comparison becomes difficult or misleading.

---

## 41. Relationship to Follow-Up

Audio calibration directly affects the Follow-Up system.

HEAR should not recommend consequential next steps based on technically weak data.

For example:

```text
One approximate speaker session
```

should not immediately produce strong follow-up messaging.

Instead:

> Repeat with headphones for a more reliable result.

By contrast:

```text
Several technically reliable sessions
showing a consistent pattern
```

can justify stronger wording such as:

> A closer look may be useful.

This creates a clean relationship:

```text
Measurement quality
        ↓
Result confidence
        ↓
Follow-up strength
```

---

## 42. Relationship to Hearing Age

The hearing-age number is especially sensitive to presentation.

It is easy for users to treat one number as absolute truth.

Therefore hearing age should always be derived from:

- known protocol version,
- known session context,
- acceptable measurement quality.

A poorly controlled session may still show an estimate, but it should be labeled appropriately.

Example:

> **Early estimate**

rather than:

> **Stable estimate**

The numeric result may be the same.

The confidence is not.

---

## 43. Long-Term Vision

HEAR should gradually improve its understanding of the complete audio chain.

The public application can begin with a controlled consumer-device screening model.

Over time it may gain:

- known-device calibration,
- better headphone support,
- richer environmental checks,
- stronger cross-session comparability.

A specialized Tier 2 environment can go much further by operating with known hearing hardware and fitting parameters.

The same game engine and UX philosophy can therefore serve two very different precision levels:

```text
HEAR Tier 1
Playful, accessible, approximate but controlled screening
```

and:

```text
HEAR Tier 2
Known hardware, stronger calibration, specialized fitting workflow
```

The visual experience may feel continuous.

The technical certainty underneath can be very different.

---

## 44. Product Principle

A concise principle for the project:

> **HEAR controls what it can, records what it cannot control, and communicates the resulting confidence honestly.**

This avoids two bad extremes.

The first extreme:

> Pretend consumer audio hardware is perfectly calibrated.

The second:

> Give up because perfect calibration is impossible.

HEAR should instead build the best controlled measurement possible from the available environment.

---

## 45. Summary

Audio output control is not an implementation detail.

It is a prerequisite for meaningful hearing results.

HEAR should therefore:

- establish a controlled system-volume range before measurement,
- keep system volume fixed during the session,
- change test intensity through HEAR stimulus amplitude,
- detect and handle volume changes,
- detect output-route changes,
- distinguish speaker and headphone sessions,
- preserve mono/stereo limitations,
- record the audio context with every result,
- model calibration quality explicitly,
- incorporate measurement quality into aggregation,
- avoid overclaiming absolute acoustic calibration,
- allow future device-specific calibration profiles,
- and use known Tier 2 hardware to achieve stronger calibration where possible.

The player should experience only a simple, calm listening setup.

The complexity belongs underneath.

That balance is essential to HEAR:

> **A playful experience on the surface, with serious measurement discipline underneath.**
