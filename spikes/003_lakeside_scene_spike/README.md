# Spike 003 — Lakeside scene, closer to the Kids Hearing Game reference

Status: built and running on real Android hardware (2026-09-22). Kept as its
own spike, separate from `spikes/002_ui_feel_spike/` (which stays as-is with
its three theme-agnostic variants — calm/energetic/dusk-fireflies).

## Goal

The project owner shared real screenshots from the Kids Hearing Game
tutorial video (purple karst mountains, huts with lit windows, willows,
palms, lily pads, a pagoda/lantern silhouette, and a real mirrored water
reflection) and asked how close pure procedural code-drawing could get to
that look, without copying it.

## What's new here versus spike 002's dusk-fireflies variant

The single biggest lever: **a genuine mirrored water reflection**, not just
a flat water color. Every composite scene element (mountains, huts, willows,
palm, lily pads, pagoda) is built as a parent `GameObject` containing all its
child sprites, then `SpawnReflection()` duplicates that whole parent via
`Object.Instantiate`, flips it vertically about the waterline
(`localScale.y *= -1`, repositioned to `2*waterLineY - originalY`), and dims
+ tints every child `SpriteRenderer` in the copy. This is what makes the
water read as a "lake" rather than a dark rectangle.

Everything else (palette, mountain silhouettes, huts, willow/palm trees,
lily pads, pagoda, combo "xN" popup UI) is still 100% procedurally drawn in
code — circles/triangles/rects composited pixel-by-pixel at runtime, same
technique family as spike 002. No external art, no image-generation tool
available in this environment.

## Honest comparison against the real game

**What worked well:**
- The reflection technique is convincing and is the standout result of this
  spike — clearly reads as a lake, not a flat panel.
- Overall palette/mood (pale lavender sky, purple mountains, warm hut
  windows) is meaningfully closer to the reference than spike 002's variants.
- Combo "xN" popup + numeric score UI matches the reference's UI style.

**What doesn't hold up:**
- Willow "hanging fronds" read more like grass/reeds at the base than
  elegant drooping branches — attempted one fix pass (hang from canopy
  underside instead of canopy center) but didn't fully solve the read.
- Palm fronds overlap into a slightly cluttered dark-green mass rather than
  distinct fan-shaped leaves.
- Mountains are sharp flat triangles — stylized, not the eroded/organic
  karst look of the reference.
- No voice narration was attempted — no text-to-speech/voice-synthesis tool
  is available in this environment, only the synthesized tone/chime approach
  already used in spike 002.

**Overall assessment:** this is meaningfully closer to the reference's mood
than spike 002, and proves real reflection is achievable with pure code, but
there's a real, honest gap between "procedurally drawn vector shapes" and
"painted/rendered 3D game art" that more iteration on shape-drawing code
alone won't fully close — actual art assets or an image-generation pipeline
would be the next lever, neither of which is available in this environment.
