# HEAR Results Handoff v1.0

This package contains the current design handoff for the **Results** section of the HEAR app.

## Goal
Build a **single reusable Results screen system** that works in three contexts:

1. **Overall Results** (entered from Home / bottom navigation)
2. **Tide Troubles post-session Results**
3. **River Journey post-session Results**

The intent is **not** to design three unrelated screens. The correct direction is:

> one Results template, three contexts.

The structure should be almost identical across all three versions. The main differences are:
- background atmosphere,
- primary summary content,
- world-specific supporting content,
- wording about session vs overall state.

## Approved primary reference
Use this file as the main visual target:

- `previews/results-scroll-board-approved.png`

It shows all three contexts side by side and, importantly, demonstrates:
- a **narrow mobile layout**,
- a **vertical scrollable page**,
- what is visible **on open**,
- what appears **after scrolling**.

## Supporting references
Use these as mood / structure support only:

- `references/results-three-contexts-supporting.png`
- `references/overall-aurora-results-reference.png`
- `references/tide-troubles-results-reference.png`
- `references/river-journey-results-reference.png`
- `references/results-empty-state-reference.png`

## Core product logic

### Shared Results philosophy
All three screens should feel like the same place in the app.
The user should not have to re-learn the UI.

The Results experience should answer two kinds of questions:

1. **How am I doing overall?**
2. **What did this specific session show?**

### Overall Results
This is the neutral long-term Results screen.

- Use an **aurora / calm neutral background**.
- This screen should emphasize the user's **overall / aggregated hearing state**.
- It should still show **Measurement quality**, because this is a key signal encouraging repeat play and better baseline building.
- Important: if the user has only e.g. `7 of 10 reliable sessions`, do **not** show a green “done” checkmark. This is progress, not completion.
- Better wording examples:
  - `Building your baseline`
  - `7 of 10 reliable sessions`
  - `More sessions will improve confidence`
  - `Stable estimate` only once truly earned.

### Post-session Results
These appear right after finishing a world.

- Keep the same layout.
- Use the world atmosphere as the background.
- The top card should describe **this session**.
- Also keep the user's **overall** result visible somewhere near the top, so the player can compare current session vs baseline.

### Audiogram / Hearing profile
Show it **every time**.
That is currently the preferred direction.

- On Overall Results: aggregated / long-term profile.
- On Post-session Results: current session profile plus relationship to overall profile.

### Tone and wording
Avoid judgmental language.
Do **not** say:
- `Good job`
- `Bad result`
- or anything that sounds like blaming the user for hearing difficulty.

If age-based comparison is shown, only do so once the app actually knows the user's age.
Until then, do not compare against an age group.

## Layout rules

### Visible on open
Only the most important information should be visible immediately.

Recommended visible-on-open sections:
1. Header / title
2. Primary summary card
3. Hearing profile preview
4. Measurement quality card

### After scroll
Additional detail should appear below the fold.

Recommended scroll sections:
1. Full hearing profile
2. Progress / trend
3. Recent sessions
4. Context-specific extra section
5. Optional follow-up / actions

## Why a scroll layout
The app runs on mobile and can have relatively narrow screens.
Trying to show everything at once makes the page too dense.
Therefore the Results page should be a **long clean scrolling page with clearly separated cards**.

## Styling direction
- One theme for now; do not split into full light/dark mode implementation yet.
- Strong readability first.
- Light / translucent cards over atmospheric backgrounds.
- Clear hierarchy.
- Clean charts.
- Calm spacing.
- Keep the page somewhere between **game** and **amateur hearing self-check**.

## Implementation summary
The best engineering direction is:
- one reusable Results page,
- same component structure in all three contexts,
- context enum or view-model decides data and background,
- vertical scrolling,
- mobile-first narrow width.
