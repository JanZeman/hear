# Reusable world presentation framework

The first implementation goal is not three production scenes. It is one reusable world contract demonstrated by three intentionally different prototypes.

## Suggested responsibility split

### Hearing / Session Engine

Owns:

- tone/catch trial schedule,
- stimulus timing,
- audio routing,
- tap/no-tap capture,
- classification,
- measurement data,
- session progression.

### Game Shell

Owns:

- startup and world selection,
- audio-output choice,
- navigation,
- global UI and brand,
- results/settings,
- transitions between shell and worlds.

### World Presentation

Owns:

- ambient visuals,
- world-specific outcome events,
- scene/session presentation progression,
- responsive world composition,
- reporting when it is safe to start another listening window.

## Conceptual interface

Adapt to the repo architecture; do not create unnecessary abstraction merely to match this exact signature.

```csharp
public interface IGameWorld
{
    Task InitializeAsync(WorldContext context);
    Task EnterListeningSafeStateAsync();
    Task PresentOutcomeAsync(OutcomePresentationContext outcome);
    Task SetSessionProgressAsync(float normalizedProgress);
    Task CompleteSessionAsync(SessionResult result);
}
```

The important part is separation, not the exact API.

## Listening-safe handshake

Outcome presentation can be visually rich, but the next quiet listening window must not overlap its most distracting phase.

A world should either:

- await completion of the disruptive portion of feedback, or
- explicitly signal `ListeningSafe`.

Do not serialize the entire world unnecessarily; ambient motion may continue.

## Generic outcome philosophy

Do not create a generic `RewardSystem` that assumes collecting, lighting or scoring.

The generic concept is **World Event / Outcome Presentation**.

Examples:

- Tide: comic capture/gag.
- Paper Garden: puppet/story action.
- River: paddle impulse/locomotion.
