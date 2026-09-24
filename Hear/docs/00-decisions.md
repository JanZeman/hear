# Current decisions

## Product identity

- Working name: **HEAR**.
- Claim: **Sound opens worlds.**
- Tone: light, playful, non-corporate, highly professional; not child-only and not medical-looking.
- Audience: broad age range. Some worlds may be calm and adult, others playful or action-oriented.
- The app should feel like a game first.

## Brand

- Primary static mark: four elements aligned to the four letters H-E-A-R.
  - H: tiny dark dot.
  - E: small pale ellipse.
  - A: medium blue ellipse.
  - R: large blue/violet ellipse.
- Preferred palette: mostly **Pure** pearl/light tones with a subtle **Aurora** blue-violet cast. Saturation may increase during active moments.
- Static identity uses the four-element ellipse mark.
- Sound-wave / opening behavior is motion language, not the permanent static logo.
- Companion/mascot is separate from the logo but derives from the final ellipse later in the experience.
- Companion should be pearl/light with aurora tint, not dark brown/black. Its shape should stay abstract and friendly rather than resembling a specific real-world product.

## App opening

- Keep splash extremely short: brand mark animation, no tutorial wall.
- First meaningful screen is the world carousel / world selection: a visual wow moment.
- World order is stable, but on a cold start the initially selected world may be randomized.
- Selecting a world happens before headphone guidance.
- Only after the user wants to enter a world do we explain that headphones are recommended and why.

## Platforms

- Mobile portrait, square/foldable, mobile landscape.
- macOS and Windows as normal resizable desktop applications.
- No forced orientation.
- Desktop window should remain usable while resizing continuously from wide landscape to narrow portrait, until a defined minimum window size.

## Three prototype worlds

- 2D: **Tide Troubles**.
- 2.5D: **The Paper Garden**.
- 3D: **River Journey**.

## Core architectural principle

**The hearing/session engine measures. The world presents. The shell navigates.**

A world must not need to know when the auditory stimulus begins. It receives classified outcomes only after the response window is resolved.
