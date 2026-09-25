# Follow-Up: Turning HEAR Results Into Meaningful Next Steps

## Purpose

HEAR should not end with a score.

The core idea of HEAR is that a person spends a few enjoyable minutes inside a game world and, almost incidentally, learns something meaningful about their hearing. The player knows that HEAR includes a hearing screening component, but the experience should never feel clinical, intimidating, or like a traditional medical test.

That creates an important product question:

> What should happen after HEAR has produced a result?

A result without context is interesting once. A result with a sensible next step gives the product purpose.

This document defines the proposed **Follow-Up** concept for HEAR: how results should lead to useful, proportionate actions without turning HEAR itself into a medical application, without stigmatizing the player, and without pushing every user toward the same outcome.

The central idea is simple:

> HEAR should help the user understand the result, decide whether any follow-up is useful, and then make the next step easy.

For some users, the right next step is simply to play another world later.

For others, the right next step may be to add their age for more context, repeat the screening, find a hearing professional, continue in another compatible application, or share HEAR with someone else.

This moves HEAR from being only a playful screening experience toward being an **entry point into a broader hearing journey**.

---

## 1. Product Positioning

HEAR sits deliberately between two categories:

- a game,
- and an informal hearing screening experience.

It should preserve the strengths of both.

From the game side, HEAR should remain:

- inviting,
- visually rich,
- low-pressure,
- replayable,
- pleasant enough that someone might use it simply because it is enjoyable.

From the hearing side, HEAR should produce information that is:

- meaningful,
- understandable,
- repeatable,
- comparable across sessions,
- useful enough to support a decision about whether to do anything next.

The follow-up system should preserve that balance.

HEAR should **not** become a clinical dashboard, diagnostic tool, hearing-aid fitting application, or professional workflow system.

Instead, HEAR should provide a clean handoff from:

> "I played something enjoyable"

into:

> "I learned something useful about my hearing"

and, where appropriate:

> "I know what I can do next."

---

## 2. The Core Result Model

The result system should distinguish between several different concepts that must not be mixed together.

### 2.1 Session result

A **session result** is the outcome of one specific play session in one specific world.

Example:

> Tide Troubles  
> This session: hearing age 41  
> Measurement quality: Good

The session result answers:

> "What did this particular play session indicate?"

It belongs naturally on the world-specific post-session screen.

The session result may also contain:

- the hearing profile from this session,
- left/right/both channel information,
- session measurement quality,
- game statistics such as fish caught,
- reaction-related game statistics,
- world-specific achievements or playful feedback.

The hearing result remains the important information. Game statistics are secondary.

### 2.2 Overall hearing estimate

The **overall hearing estimate** is the user's current longer-term result based on multiple reliable sessions.

Example:

> Your hearing age: 39  
> Stable estimate  
> Based on 10 reliable sessions

This answers:

> "What does HEAR currently think my hearing looks like overall?"

This is the primary value shown on the persistent Results screen.

It should not necessarily be a simple arithmetic average of all session results.

A future scoring implementation may prefer a more robust aggregation method that can account for:

- unreliable sessions,
- environmental interruption,
- false positives,
- missed interactions,
- unusual outlier sessions,
- left/right differences,
- measurement quality.

The exact statistical method is a separate implementation decision.

The product requirement is that the user must be able to distinguish clearly between:

- the latest session result,
- and the current overall estimate.

### 2.3 Hearing profile

The **hearing profile** explains how hearing performance varies across frequencies.

This is a more informative result than a single hearing-age number.

The UI should initially call this a **Hearing profile**, rather than presenting it as a clinical audiogram.

Reasons:

1. The current product is explicitly not intended to present itself as a medical application.
2. The current screening protocol is still a simplified prototype.
3. "Hearing profile" is understandable to a broad audience.
4. It allows HEAR to show useful frequency information without suggesting a clinical diagnosis.

The hearing profile should be shown in two contexts.

#### Post-session

The graph represents the result of **this session**.

Where useful, HEAR may also overlay the user's current overall profile in a visually secondary form.

Example:

- solid line = this session,
- subtle/dashed line = current baseline.

This allows the user to see whether the latest session was broadly consistent with previous sessions.

#### Persistent Results

The graph represents the **aggregated hearing profile** based on reliable sessions.

This is the user's current overall profile.

### 2.4 Reliability / baseline state

A result should always communicate how established it is.

Suggested states:

#### No result

No reliable session has been completed.

#### First estimate

Exactly one reliable session exists.

Example:

> First estimate  
> Based on 1 reliable session

#### Building your baseline

Several sessions exist, but the result is not yet considered stable.

Example:

> Building your baseline  
> Based on 3 reliable sessions

#### Stable estimate

Enough reliable sessions exist for HEAR to present the result with greater confidence.

Example:

> Stable estimate  
> Based on 10 reliable sessions

This is important because a user should not interpret one play session as equivalent to a result supported by repeated measurements.

### 2.5 Trend

Once multiple reliable sessions exist, HEAR can show change over time.

The trend should answer:

> "How have my results changed across sessions?"

It should not imply that every small movement represents a meaningful physiological change.

The presentation should therefore be calm and descriptive.

Preferred wording:

- "Your recent results have been consistent."
- "Recent sessions have varied slightly."
- "Higher frequencies have required a little more volume in recent sessions."

Avoid overly evaluative wording such as:

- "You improved!"
- "You got worse!"
- "Great job!"
- "Poor result!"

Hearing ability is not an achievement or moral outcome.

---

## 3. The Follow-Up Principle

The follow-up system should respond to the user's result without labeling the person as "good" or "bad."

The product should classify what **action may be useful**, not classify the user.

A useful conceptual model is:

### State A: Result not yet established

The estimate is based on too little reliable data.

Primary action:

> Play another world / repeat another reliable session.

The goal is to establish a baseline.

### State B: Stable result with no strong reason for follow-up

The result is reasonably stable and does not currently suggest a need for stronger follow-up messaging.

Primary actions may include:

- explore another world,
- check results again later,
- share HEAR,
- optionally add age for more context.

The result screen should remain informative, not congratulatory.

### State C: Stable result where further checking may be useful

If repeated results consistently indicate that some sounds require significantly more volume, HEAR may suggest a next step.

The tone must remain calm.

Example:

> A closer look may be useful  
> Your recent sessions consistently show that some sounds require more volume. A professional hearing check can give you a clearer picture.

Possible actions:

- Find a hearing professional
- Continue in a compatible follow-up application
- Learn more
- Repeat the screening later

HEAR should not present the result as a diagnosis.

---

## 4. Why "Good Job" / "Bad Job" Is Wrong

HEAR should not praise or blame a player for their hearing result.

This includes messages such as:

- "Great job!"
- "Excellent hearing!"
- "Bad result"
- "Try harder"
- "You failed"

A person does not earn better hearing through performance.

Praise may still be appropriate for **game behavior**:

- completing a world,
- finding a hidden item,
- finishing a challenge,
- reaching a game milestone.

But hearing-related language must remain descriptive.

Good:

> Good measurement quality

This evaluates the **quality of the measurement**, not the person.

Good:

> Higher frequencies required a little more volume.

Good:

> Your result is similar to your recent sessions.

Avoid:

> Your hearing is bad.

Avoid:

> You performed poorly.

This distinction should be treated as a permanent copywriting rule.

---

## 5. Age as Optional Context

HEAR does not need the user's age before the first game.

This is important.

The player should be able to start quickly without being asked for personal information upfront.

After the first reliable result, HEAR can offer age as an optional enhancement.

Example:

> Want more context?  
> Add your age and HEAR can put your hearing estimate into perspective.

Actions:

- Add my age
- Not now

The user should be free to skip this indefinitely.

### Why ask after the first session?

Before the first session, the user has not yet received value from HEAR.

Asking for personal information at that stage introduces friction.

After the first session:

- the user understands the product,
- HEAR has delivered value,
- the reason for asking becomes clear.

The request becomes contextual rather than administrative.

### 5.1 What age enables

If age is known, HEAR can provide additional interpretation.

For example:

> Your age: 52  
> Your current hearing age: 39

Potentially:

> Your hearing estimate is 13 years younger than your age.

However, age comparison must only be shown if the comparison is supported by the actual scoring model and appropriate reference data.

Until such reference data exists, HEAR should not invent statements such as:

> "14 years better than your age group."

Age should enrich interpretation only when the underlying method supports it.

---

## 6. Sharing HEAR

Sharing should be part of the positive path of the product.

If no follow-up is currently suggested, HEAR can offer:

> Know someone who might enjoy HEAR?  
> Share HEAR

The wording should remain neutral.

Avoid:

> Share this with someone whose hearing is bad.

That would reintroduce the stigma HEAR is trying to avoid.

The user can decide privately why they want to share the app.

Possible reasons include:

- they enjoyed it,
- they think someone else would enjoy it,
- they are curious about another person's result,
- they suspect someone may benefit from checking their hearing.

HEAR does not need to state the reason.

### 6.1 Share HEAR vs Share My Result

These should be separate actions.

#### Share HEAR

Shares:

- the app,
- a store link,
- an invitation,
- possibly a playful world-themed message.

No personal hearing result is included.

#### Share My Result

Explicitly shares hearing-related information chosen by the user.

This should require a clear intentional action.

A future result share card might include:

- hearing age,
- hearing profile summary,
- date,
- measurement quality.

The user must control exactly what is shared.

---

## 7. Finding Hearing Professionals

When follow-up may be useful, HEAR can help the user find an appropriate professional nearby.

This may become one of the most important long-term functions of the product.

A result should not simply say:

> "See a professional."

That creates friction and often ends the journey.

A better flow is:

> A professional hearing check could give you a clearer picture.

Action:

> Find hearing professionals

This could eventually open:

- a map,
- a nearby-provider search,
- a curated provider list,
- a supported external directory.

The exact provider discovery implementation can evolve independently from the Results UI.

The important product principle is:

> HEAR should reduce the distance between insight and action.

---

## 8. Tier 2 / Follow-Up Applications

The most significant extension of the Follow-Up concept is the ability for HEAR to continue into another application.

This is the original Tier 2 idea in a more general form.

### Tier 1: HEAR

HEAR remains:

- a standalone game,
- a screening experience,
- brand-neutral at the platform level,
- simple,
- broadly accessible,
- non-clinical in presentation.

### Tier 2: Specialized follow-up application

A separate application may provide deeper functionality.

For example:

- professional hearing follow-up,
- hearing-aid-specific workflows,
- self-fitting functionality,
- manufacturer-specific functionality,
- clinician-supported workflows,
- richer diagnostics,
- device configuration.

In one concrete future scenario, a Tier 2 application could be developed within a hearing-aid company and provide a professional or self-fitting workflow for supported hearing aids.

HEAR itself should not contain that specialized functionality.

Instead, HEAR should be able to say:

> A compatible follow-up app is available.

and offer:

> Continue in [Application]

This allows HEAR to remain simple while still participating in a richer ecosystem.

---

## 9. Do Not Make HEAR a Hard-Coded Vendor Funnel

HEAR should not know about one specific hearing-aid brand at the architectural core.

The integration model should be generic.

Conceptually, HEAR should understand:

> "This provider can handle hearing follow-up."

rather than:

> "Launch Company X's hearing aid application."

A future internal model might look conceptually like:

```text
FollowUpProvider
    id
    displayName
    capabilities
    supportedData
    launchUri
    installUri
```

Possible capability examples:

```text
professional-search
hearing-follow-up
hearing-aid-fitting
device-configuration
extended-screening
```

Possible supported data:

```text
session-hearing-estimate
overall-hearing-estimate
frequency-profile
left-right-profile
measurement-quality
session-history-summary
```

The exact API is not defined here.

The important architectural requirement is that the integration remain provider-neutral.

---

## 10. "Plugin" Does Not Need to Mean Dynamic Code Plugin

Product language and technical implementation should be separated.

From the user's perspective, a compatible application may feel like an extension or plugin.

Technically, HEAR does not need to load third-party code into the HEAR process.

A safer and simpler architecture is:

1. compatible application registers that it can handle a HEAR follow-up flow,
2. HEAR detects or resolves that capability,
3. HEAR offers the provider on the Results page when appropriate,
4. user explicitly chooses the provider,
5. HEAR launches the other application through a supported platform mechanism,
6. user explicitly approves any hearing data transfer.

Potential platform mechanisms may include:

- deep links,
- universal/app links,
- custom URL schemes,
- Android intents,
- platform-specific app association mechanisms.

The HEAR domain model should hide these platform differences behind a shared abstraction.

---

## 11. Explicit User-Controlled Data Handoff

HEAR should never silently transfer hearing-related results to another application.

A handoff should be explicit.

Example:

> Continue in Acme Hearing  
> This app can use your latest HEAR profile as a starting point.

Then show what is being shared:

- latest hearing estimate,
- frequency profile,
- left/right information,
- measurement quality.

Possible actions:

- Continue and share
- Continue without sharing, if supported
- Cancel

The user should remain in control.

This is both a trust principle and a product-design principle.

---

## 12. Provider Discovery

HEAR may eventually support multiple follow-up providers.

The UI should not become a marketplace.

The Results page should show only providers that are relevant to the user's current context.

Example:

> Next steps

> Find a hearing professional

> Continue in Acme Hearing  
> Compatible app installed

If no compatible application is installed, HEAR may optionally offer:

> Get Acme Hearing

but only when that provider is relevant to the follow-up context.

Provider ranking and commercial relationships are intentionally outside the scope of this document and would require separate product rules.

---

## 13. The "Next Steps" Results Section

The Follow-Up system should appear as a clear section near the bottom of the long Results page.

The Results page may therefore follow this general structure:

### 1. Your hearing age
Overall estimate.

### 2. Hearing profile
Aggregated frequency-based profile.

### 3. Reliability
First estimate / building baseline / stable estimate.

### 4. Age context
Only if age is known, or an invitation to add it.

### 5. Trend
When enough sessions exist.

### 6. Latest session
Most recent world and session result.

### 7. Session history / game information
Secondary information.

### 8. Next steps
Contextual actions based on the result.

This section changes dynamically.

---

## 14. Examples of "Next Steps"

### 14.1 No result yet

> **Build your first result**  
> Play a world to create your first hearing estimate.

Action:

> Choose a world

### 14.2 One reliable session

> **Build your baseline**  
> Your first estimate is ready. A few more sessions will help HEAR understand how consistent it is.

Actions:

> Play another world  
> Add my age for more context

### 14.3 Several consistent sessions

> **Keep listening**  
> Your recent results have been consistent.

Actions:

> Play another world  
> Share HEAR

Optional:

> Add my age for more context

### 14.4 Follow-up may be useful

> **A closer look may be useful**  
> Your recent sessions consistently show that some sounds require more volume. A professional hearing check can give you a clearer picture.

Actions:

> Find a hearing professional  
> Continue in compatible app  
> Learn more

HEAR should not use alarmist presentation.

No red warning panel is required merely because follow-up is suggested.

---

## 15. Post-Session Follow-Up vs Persistent Results

The product has two different result contexts.

They should share data but not necessarily presentation.

### 15.1 Post-session result

Shown immediately after completing a world.

Visual language:

- world-specific,
- emotionally continuous with the game,
- Tide Troubles remains Tide Troubles,
- River Journey remains River Journey,
- Paper Garden remains Paper Garden.

Primary question:

> "What did this session show?"

Typical content:

- This session: hearing age
- This-session hearing profile
- Measurement quality
- Overall hearing age
- Game statistics
- View full results
- Continue playing

The world-specific screen should preserve the visual reward of having completed the game.

### 15.2 Persistent Results tab

Opened from Home / bottom navigation later.

Visual language:

- neutral HEAR environment,
- aurora / polar-lights theme,
- calm,
- world-neutral.

Primary question:

> "What does HEAR currently know about my hearing overall?"

Typical content:

- overall hearing age,
- aggregated hearing profile,
- reliability / baseline state,
- trend,
- latest session,
- history,
- age context,
- next steps.

The neutral aurora background separates long-term Results from individual game worlds.

---

## 16. Navigation Philosophy

Results should not become a complex hierarchy of tabs.

The preferred model is:

> one long, clearly sectioned scrolling Results page.

Users should scroll when they are simply seeing more information about the same overall result.

Navigation is appropriate only when the **subject changes**.

Examples where navigation makes sense:

### Overall Results -> Specific session

The user changes from:

> "How is my hearing overall?"

to:

> "What happened in Tide Troubles on 24 September?"

A dedicated Session Detail screen is appropriate.

### Results -> Professional search

The user changes from viewing a result to finding a provider.

A separate screen or external flow is appropriate.

### Results -> Settings / data management

Resetting hearing history is a different task and belongs in Settings or a confirmation sheet.

### Results -> Provider application

Launching a Tier 2 application is explicitly leaving HEAR's primary result experience.

Navigation/handoff is appropriate.

---

## 17. What Should Usually Stay on the Same Scroll Page

The following information does not require separate navigation:

- hearing age,
- hearing profile,
- baseline state,
- age context,
- trend,
- latest session summary,
- game stats,
- next-step recommendation.

They are all different aspects of the same overall result.

A card-based long-scroll layout allows HEAR to show substantial information without becoming difficult to navigate.

---

## 18. Resetting Hearing History

Users should be able to start over.

However, reset should not be a prominent action on the main Results page.

Suggested location:

> Settings -> Hearing data -> Reset hearing history

Or:

> Results -> Data & history -> Reset

The action should clearly explain what is affected.

Potential model:

### Reset hearing results

Clears:

- session hearing estimates,
- aggregate hearing estimate,
- hearing profile,
- trend,
- baseline state.

Keeps:

- game achievements,
- world progress,
- cosmetic unlocks,
- unrelated app settings.

### Reset game progress

Separate action.

### Reset everything

Optional advanced action.

Separating hearing history from game progress is important because the user may want to establish a new hearing baseline without losing game progress.

---

## 19. Measurement Quality Is Not Hearing Quality

The phrase **Good measurement** or **Good measurement quality** is acceptable.

It refers to the reliability of the session.

It must never be confused with:

> good hearing.

Measurement quality may consider future signals such as:

- excessive false positives,
- interrupted session,
- inconsistent reactions,
- insufficient data,
- environmental noise,
- incomplete left/right coverage.

Possible states:

- Good measurement
- Limited measurement
- Repeat recommended

These are measurement states, not judgments of the user.

---

## 20. Result Language Principles

### Prefer descriptive language

Good:

> Higher frequencies required a little more volume.

Good:

> Your recent sessions have been consistent.

Good:

> This is your first estimate.

Good:

> A professional check could provide a clearer picture.

### Avoid judgmental language

Avoid:

> Great hearing!

Avoid:

> Poor hearing.

Avoid:

> Bad score.

Avoid:

> Great job!

Avoid:

> You failed.

### Avoid unnecessary clinical framing

Prefer:

> Hearing profile

over prematurely presenting:

> Diagnostic audiogram

unless the underlying product later evolves and has a justified reason to use clinical terminology.

---

## 21. Purpose of Follow-Up

The Follow-Up system changes the role of HEAR.

Without follow-up:

> HEAR is a pleasant hearing-screening game that produces an interesting result.

With follow-up:

> HEAR becomes an approachable front door to hearing awareness.

The user may:

- discover that nothing further seems necessary,
- establish a longer-term baseline,
- monitor results over time,
- gain useful age context,
- share the experience,
- find a hearing professional,
- continue into a specialized application,
- eventually enter a hearing-aid workflow.

HEAR itself can remain simple.

The ecosystem around HEAR can become sophisticated.

---

## 22. Strategic Value

This architecture creates several important opportunities.

### 22.1 HEAR remains broadly accessible

The first experience stays simple and attractive.

No manufacturer, provider, clinic, or device relationship is required.

### 22.2 Specialized functionality can live elsewhere

Advanced hearing-aid features do not need to burden HEAR.

A Tier 2 application can evolve independently.

### 22.3 HEAR can become an ecosystem entry point

Multiple future follow-up providers may integrate with HEAR.

HEAR can remain the common first step.

### 22.4 The user remains in control

Nothing happens automatically.

The player decides whether to:

- add age,
- repeat a session,
- share,
- search for a professional,
- open another application,
- transfer hearing-related data.

### 22.5 The product gains a clear purpose beyond entertainment

The game's fun remains essential.

But the product's deeper purpose becomes:

> Make it easy for people to notice, understand, and act on changes in their hearing without making the first step feel clinical, stigmatizing, or difficult.

That is a substantially stronger product vision than simply:

> "A game that estimates your hearing age."

---

## 23. Proposed Long-Term User Journey

A representative journey could look like this:

### Step 1

User discovers HEAR and starts immediately.

No age requested.

No account required solely to begin playing.

### Step 2

User plays Tide Troubles.

### Step 3

Post-session result appears.

> This session: 41 years

HEAR also shows the session hearing profile.

### Step 4

HEAR explains:

> First estimate  
> Play a few more sessions to build your baseline.

### Step 5

HEAR optionally offers:

> Add your age for more context.

User may skip.

### Step 6

User plays River Journey and Paper Garden over the following days.

### Step 7

Persistent Results now shows:

> Your hearing age: 39  
> Stable estimate  
> Based on 5 reliable sessions

Along with:

- overall hearing profile,
- trend,
- latest session,
- age context if available.

### Step 8A

If no follow-up is currently suggested:

> Keep listening

Possible actions:

- Play another world
- Share HEAR

### Step 8B

If repeated results indicate further checking may be useful:

> A closer look may be useful.

Possible actions:

- Find a hearing professional
- Continue in compatible app
- Learn more

### Step 9

User explicitly selects a follow-up provider.

If a compatible Tier 2 application exists, HEAR offers a data handoff.

### Step 10

The specialized application takes over.

HEAR has completed its job.

---

## 24. Architectural Boundary

HEAR's responsibility should end at a clear boundary.

HEAR owns:

- game worlds,
- sound presentation,
- reaction detection,
- session screening result,
- aggregate screening result,
- hearing profile,
- trend,
- result explanation,
- follow-up discovery,
- explicit result handoff.

A Tier 2 provider may own:

- device-specific behavior,
- hearing-aid fitting,
- professional workflows,
- extended assessments,
- clinical workflows,
- manufacturer-specific account/device ecosystems.

This boundary should be deliberate.

---

## 25. Future Design Work

This document defines the product direction, not the final implementation.

Future work should include:

### Result classification

Define exactly when HEAR considers an estimate:

- first,
- building,
- stable,
- follow-up-worthy.

### Aggregation

Define how multiple sessions produce:

- hearing age,
- frequency profile,
- left/right profile,
- trend.

### Age context

Define what comparisons are scientifically and statistically justified.

### Provider contract

Design the actual `FollowUpProvider` model.

### Data handoff schema

Define what can be passed to compatible applications.

### Platform integration

Prototype:

- iOS app links,
- Android intents/app links,
- installed-provider detection,
- installation fallback.

### Professional search

Decide whether HEAR:

- implements search itself,
- opens a provider directory,
- uses a third-party service,
- allows provider plugins.

### Privacy / consent

Define the user-facing consent flow for:

- age,
- history,
- result sharing,
- cross-app handoff.

### Visual design

Create final designs for:

- neutral aurora Results,
- world-specific post-session Results,
- Next Steps states,
- provider cards,
- professional search,
- share flow.

---

## 26. Open Questions

Several important questions remain intentionally unresolved.

1. What exact measurement conditions trigger a follow-up recommendation?
2. How many reliable sessions are required before an estimate becomes "stable"?
3. How should outlier sessions affect the aggregate?
4. Should HEAR ever display normative age-group comparison?
5. What external reference data would be required to support that comparison?
6. Should professional search be location-based inside HEAR or delegated externally?
7. Can multiple follow-up providers appear simultaneously?
8. How are providers ranked?
9. Which hearing data may be exported?
10. What data format should Tier 2 applications accept?
11. How should HEAR behave if a Tier 2 application is installed but incompatible with the current result format?
12. Should users be able to export a portable result file independently of any provider?
13. Should session history remain entirely local by default?
14. What wording best communicates that HEAR is informative but not a medical diagnosis?
15. What should happen if left and right hearing profiles differ significantly?

These questions should be handled in future design notes rather than solved implicitly in UI code.

---

## 27. Product Principles to Preserve

The following principles should remain stable even as implementation changes.

### HEAR delivers value before asking for personal data.

### Results describe hearing; they do not judge the user.

### The player can understand the difference between one session and their overall result.

### Repeated play improves the quality of the estimate.

### Results remain readable and friendly.

### The app can recommend a next step without pretending to diagnose.

### Follow-up is contextual, not mandatory.

### The user explicitly controls sharing and cross-app handoff.

### HEAR remains provider-neutral at its architectural core.

### Specialized Tier 2 functionality stays outside the core HEAR application.

### The fun experience is not decoration; it is the mechanism that makes hearing awareness approachable.

---

## 28. Vision

HEAR should make hearing awareness feel ordinary.

Not clinical.

Not embarrassing.

Not something that begins only after a person believes something is wrong.

A person should be able to open HEAR because the game looks interesting, spend a few enjoyable minutes inside a world, and leave with a better understanding of their hearing.

If everything appears consistent, HEAR can simply invite them back later or encourage them to share the experience.

If repeated results suggest that a closer look may be useful, HEAR should make that next step obvious and easy.

And if a richer application or professional workflow is available, HEAR should be able to hand over gracefully without trying to become that application itself.

That is the purpose of Follow-Up:

> **Turn hearing awareness into a useful next action, while keeping the first step playful, calm, voluntary, and human.**
