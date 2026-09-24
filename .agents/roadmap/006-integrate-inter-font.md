# Integrate the Inter typeface instead of the default runtime font

**Status**: Open
**Milestone**: Vertical slice
**Depends on**: -

## What needs to happen

`Hear/docs/14-visual-bible.md` (section 5, Typography) already specifies the brand typeface as
Inter, and explicitly says: "If Inter is not already available, do not block architecture work to
obtain it. Use a neutral sans-serif placeholder..." - so the shell has been running on Unity's
default runtime font (no custom Font Asset anywhere in `Assets/`) as a deliberate, documented
placeholder, not an oversight. Human feedback 2026-09-25 ("Fonty napisu vubec nesedi") confirms
the gap is now worth closing: bring in Inter (OFL-licensed, freely embeddable) as a Unity Font
Asset and wire it through `VisualTokens.Type` so every `Label` uses it instead of the default.

## Definition of done

- [ ] Inter (regular + the weights `VisualTokens.TypeStyle` needs) imported as Unity Font Asset(s).
- [ ] `VisualTokens`/`ShellUIController` labels use it via `unityFont`/`unityFontDefinition`.
- [ ] Visually compared against `sources/HEAR-App-UI-Home.png` on-device.

## Notes

- 2026-09-25: Logged per human instruction to track feedback that can't be implemented in the
  current work session (`AB-ROADMAP-005`). Not started - needs sourcing the actual font files
  before any code change.
