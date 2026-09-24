# macOS and Windows behavior

HEAR should be a normal resizable desktop app on macOS and Windows, not a fixed mobile viewport embedded in a desktop window.

## Navigation

Do not rely on OS menus as the only app navigation.

### Wide desktop window

Use a compact left sidebar for main in-app destinations.

Recommended initial information architecture:

- Worlds
- Results
- Settings

Avoid duplicating Home and Worlds unless the product later gains a distinct dashboard.

### Medium width

Collapse sidebar to icon rail.

### Compact / narrow portrait-like desktop window

Move the same primary destinations to bottom navigation, matching the mobile mental model.

## Native OS chrome

### macOS

Use normal macOS window controls and system menu bar. System menus can expose platform actions such as About, Settings, View, Window, Help and keyboard shortcuts.

### Windows

Use normal Windows window chrome and expected window actions. A native-style title/command area is fine, but it does not replace the in-app navigation.

## Principle

OS menu = application/platform commands.  
HEAR navigation = moving inside the product.

Reference only: `assets/reference/ui/desktop-responsive-concept.png`. Some IA labels in that concept image are exploratory and are superseded by this document.
