# Game shell and first-run flow

## Global shell

Worlds are visually free. The shell is consistent.

The shell owns:

- brand / logo,
- typography,
- buttons and iconography,
- world selection,
- headphone/speaker choice,
- instructions,
- results,
- settings,
- navigation and transitions.

Worlds own their fantasy and presentation.

## Startup flow

### 1. Short splash

Target: roughly 0.5–1 second when loading permits.

Possible motion:

```text
dot -> 3 expanding ellipses -> HEAR lockup -> world selector
```

Keep it visually clean. No tutorial text.

### 2. World selector: first real screen

- This is the wow screen.
- Stable world order.
- Random active index on cold start.
- Returning from a world keeps the currently selected world.
- Active world strongly influences the background.
- Neighbouring worlds are visually discoverable by carousel affordance.
- Do not show implementation terms such as 2D / 2.5D / 3D to users.

### 3. User selects a world

Only now show a lightweight choice:

**Headphones recommended**  
More precise; left and right can be tested separately.

- Continue with headphones
- Play through phone / computer speakers

Do not block play when headphones are unavailable.

### 4. Micro-instruction

Prefer a single in-world instruction such as:

> Tap when you hear the tone.

Dismiss automatically after the user demonstrates the mechanic.

### 5. Play

Minimize shell UI during gameplay.

### 6. Results

Use the same HEAR shell regardless of world. Detailed result semantics remain a separate product/science decision; presentation should remain friendly and non-clinical by default.
