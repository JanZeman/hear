# World 3 — River Journey

## Technology target

**3D**, with hybrid tricks allowed for distant scenery.

## Mood

Cinematic, beautiful, inviting journey by canoe through a river landscape toward and past a settlement. This is a living postcard, not an open-world navigation game.

## Camera and movement

- Fixed/follow presentation camera.
- Canoe follows a designed spline/path.
- Player does not steer.
- Real 3D for foreground/midground elements that need perspective.
- Distant mountains/sky may use cheaper 2D/skybox techniques.

## Session progression

Normalized session progress drives the journey along the river so everyone eventually reaches the destination.

Suggested story beats:

- 0–15% wilderness,
- 15–30% first signs of settlement,
- 30–50% distant village,
- 50–70% approach,
- 70–90% pass by detailed activity,
- 90–100% destination.

## Correct-detection event

On `CorrectDetection`:

```text
natural paddle stroke
+ water ripple/splash
+ short forward acceleration / momentum impulse
```

The boat still drifts/progresses with the session when detections are missed.

## Ear-aware presentation

After classification:

- Left trial may animate a left-side paddle stroke.
- Right trial may animate a right-side paddle stroke.
- Combined may choose naturally.

Do not visually indicate the side before the response.

## Responsive framing

Protect the canoe, travel direction and important route inside the Core Safe Square. Wider windows reveal more banks/scenery. Tall windows reveal more sky/water. Do not stretch the scene.

## Prototype assets

Use low-cost placeholders for geometry first. Reference mood: `assets/reference/worlds/river-journey-style-frame.png`.
