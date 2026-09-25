# Dev Agent Prompt — HEAR Results Screen v1.0

## Context
Implement the HEAR **Results** experience using the design handoff in this package.

This is **not** three different products. It is one Results screen system rendered in three contexts:
- Overall Results
- Tide Troubles post-session
- River Journey post-session

Use the files in this handoff as the source of truth for this round.

## Primary visual target
Use:
- `previews/results-scroll-board-approved.png`

as the main approved visual reference.

Supporting references:
- `references/results-three-contexts-supporting.png`
- `references/overall-aurora-results-reference.png`
- `references/tide-troubles-results-reference.png`
- `references/river-journey-results-reference.png`
- `references/results-empty-state-reference.png`

## Implementation goal
Build **one reusable scrollable Results page** and render it in these three contexts.

Suggested model shape:

```text
ResultsContext
    Overall
    TideTroublesSession
    RiverJourneySession
```

The page structure should stay almost identical across all three contexts. Only data, wording, and atmosphere should change.

## Required UX behavior

### 1. Mobile-first narrow layout
Design for narrow phone screens first.
Use a vertical scroll view.
Do not attempt to show too much above the fold.

### 2. On open, show only the essentials
Above the fold should mainly include:
- page title / header,
- top summary card,
- hearing profile preview,
- measurement quality card.

### 3. After scroll, show more detail
Below the fold should include:
- full hearing profile,
- progress / trend,
- recent sessions,
- context-specific supporting section,
- optional follow-up or utility actions.

## Required product logic

### Overall Results
Purpose: show the user's longer-term state.

Visual direction:
- calm neutral **aurora** background.

Content direction:
- primary hero is the **overall hearing result**,
- hearing profile is the **overall / aggregate profile**,
- measurement quality must be visible here too,
- if the user has not yet reached a stable baseline, show progress language such as:
  - `Building your baseline`
  - `7 of 10 reliable sessions`
  - `More sessions will improve confidence`

Important:
- do **not** show a green completion check if the baseline is still incomplete.
- “Stable estimate” should only appear once the required threshold is truly met.

### Post-session Results
Purpose: show what the current session produced, while keeping the long-term result visible.

Visual direction:
- use the selected world's background.
- Tide Troubles = warm harbor / sunset / playful atmosphere.
- River Journey = calm sunset / river / reflective atmosphere.

Content direction:
- top card should emphasize **This session**,
- also show the user's **overall** result nearby,
- hearing profile should still be present,
- measurement quality should describe the current session,
- lower sections can include progress and recent sessions.

## Audiogram rule
Show the hearing profile / audiogram in all three contexts.

- Overall screen: aggregate profile.
- Post-session screens: session profile, ideally in relation to the overall profile.

## Wording rules
Avoid judgmental or emotionally loaded language.

Do not use:
- `Good job`
- `Bad result`
- any wording that implies blame for hearing difficulty.

Do not compare the user with an age group unless the app actually knows the user's age.

## Theming / readability rules
- prioritize readability over visual drama,
- use light or lightly translucent cards over rich backgrounds,
- charts and text must remain clearly legible,
- do not overfill the screen,
- preserve the calm polished HEAR style.

## Empty state
If no sessions exist yet, the page should still look intentional, not empty or broken.
Use the empty-state reference as guidance and provide a clean first-use message.

## Technical recommendation
Implement the page as a reusable view/prefab with data-driven sections.
For example:

```text
ResultsScreen
    HeaderSection
    SummaryCard
    HearingProfilePreview
    MeasurementQualityCard
    ScrollContent
        FullHearingProfileCard
        ProgressCard
        RecentSessionsCard
        ContextExtraCard
        UtilityCard
```

A context/view-model should swap:
- background,
- summary title,
- summary data,
- hearing profile source,
- measurement quality wording,
- context-specific lower card content.

## Deliverable expectation
The goal of this implementation round is not perfect final polish.
The goal is a **credible first production-quality Results screen foundation** that matches the handoff structure closely and can be iterated from there.
