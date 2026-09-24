# Acceptance criteria for the first handoff milestone

The first milestone is an architecture/prototype proof, not final art.

1. All three worlds run through one shared hearing/session API.
2. The same deterministic trial sequence produces the same stored measurement/session outcome in every world.
3. World code does not need stimulus-onset presentation callbacks.
4. Ambient visual events are not synchronized to tone timing.
5. A tap anywhere in the valid response window is sufficient.
6. Catch trials work in all three worlds without a visible fake cue.
7. `CorrectDetection`, `Miss`, `FalsePositive`, and `CorrectRejection` are all representable.
8. Speaker mode uses a combined/non-ear-specific path.
9. Headphone mode supports Left/Right, and at least one prototype demonstrates post-classification lateralized feedback.
10. Session completion is independent of hearing performance.
11. World feedback and the hearing engine have a listening-safe handshake before the next trial.
12. Cold start can select any world as the active carousel item without changing stable world order.
13. The selected world changes the home-screen atmosphere/background while shell controls remain consistent.
14. Mobile portrait, square/foldable and landscape layouts are usable.
15. macOS and Windows builds support normal resizable windows.
16. Desktop navigation adapts from sidebar -> icon rail -> bottom navigation as width becomes compact.
17. Continuous window resizing does not stretch artwork non-uniformly.
18. Core gameplay composition remains valid inside a centered 1:1 safe area.
19. The app has a defined practical minimum desktop window size and handles reaching it cleanly.
20. Production art recreation is explicitly out of scope for this milestone.
