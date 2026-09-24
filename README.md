# HR — Hear

**Hear** is a short, genuinely fun mobile game that produces sounds of varying pitch and
loudness; the player reacts whenever they notice one. Under the hood, this is a hearing
screening, and the player knows it going in; it simply never feels clinical or like a medical
test, for players of any age.

## Status

This is a prototype: the shared architecture is being proven out, the art is placeholder, and the
audiometric protocol is a simplified stand-in for the real one. Nothing here is a finished,
production-ready product yet.

The app must always make it unmistakably clear to the player that Hear is not a medical
application or device, and the player must acknowledge that they understand this before any
hearing-related result is shown.

## Why

Hearing fades gradually and unnoticed, and almost nobody checks it: a "hearing test" sounds
clinical, dull, and faintly stigmatizing, something for old people, or something implying you're
already broken, and it demands booking an appointment somewhere. People do, however, happily
spend a few minutes on a well-made mobile game, purely because it's appealing, not because
someone told them it's good for them. A Hear player knows from the start that it is a hearing
test, just a fun one: they spend a few enjoyable minutes inside a small, pleasant world, then get
a real result about their hearing at the end, whether that is simply "your ear age" today or
richer results later, with no medical framing, no pressure, and no personal data demanded up
front.

See [AGENTS.md](AGENTS.md) for the agent behavior contract.

## Repository layout

- `Hear/` — the Unity project (Unity 6, Universal Render Pipeline).
- `spikes/` — archived personal visual and interaction experiments.
- `sources/` — retained reference images used during the design work.

## Scope of this repository

This repository covers **only** the standalone game experience: playing one of the game's
"worlds", producing sounds, detecting the player's reaction, and showing a friendly result
("ear age") at the end. Nothing else is in scope here — see `Hear/README.md` for the full
product description and design rules.
