#!/usr/bin/env python3
"""Bake a saturation/brightness correction into the Tide Troubles source PNGs.

Human feedback 2026-09-26: the handoff art reads as oversaturated/too bright on-device, even
after a runtime dim overlay in TideTroublesPresentation - a flat overlay tint looked worse (a
gray wash) than actually correcting the source pixels, so this replaces that runtime approach.
Alpha is left untouched; only RGB is adjusted, so transparency/cutout edges are unaffected.

Usage (from the repo root, with Pillow installed - `pip install pillow`):
    python3 Hear/docs/v1.0-tide-troubles-handoff/adjust-source-colors.py \\
        sources/hear-tide-troubles-handoff-v1.0.zip \\
        Hear/Assets/HearApp/Resources/Worlds/TideTroubles/Scene

Re-run with different SATURATION/BRIGHTNESS values below and re-import in Unity to retune; it
always starts from the original handoff zip, never from a previously-adjusted PNG, so repeated
runs never compound the effect.
"""
import os
import sys
import zipfile
import tempfile
from PIL import Image, ImageEnhance

SATURATION = 0.78  # 1.0 = unchanged; lower = less saturated
BRIGHTNESS = 0.88   # 1.0 = unchanged; lower = darker

# (path inside the handoff zip's assets/ folder, path inside Scene/ in the Unity project)
FILES = [
    ("backgrounds/sunset-harbor-lighthouse-view.png", "Backgrounds/harbor-background.png"),
    ("backgrounds/golden-hour-seaside-dock-frame.png", "Backgrounds/dock-frame.png"),
    ("sprites/fish/cute-fish-sprite-sheet-grid.png", "Sprites/fish-sheet.png"),
    ("sprites/seagulls/seagull-flight-pose-sprite-sheet.png", "Sprites/seagull-sheet.png"),
    ("sprites/dog/playful-harbor-terrier-sprite-sheet.png", "Sprites/dog-sheet.png"),
    ("sprites/companion/cute-translucent-ghost-mascot-sprite-sheet.png", "Sprites/companion-sheet.png"),
    ("sprites/floating-objects/sunset-harbor-buoy-sprite-sheet.png", "Sprites/buoy-sheet.png"),
    ("sprites/launcher/harbor-net-cannon-sprite-sheet.png", "Sprites/launcher-sheet.png"),
    ("sprites/effects/vibrant-water-splash-sprite-sheet.png", "Sprites/splash-sheet.png"),
    ("sprites/effects/golden-fishing-effects-asset-sheet.png", "Sprites/effects-sheet.png"),
]


def adjust(img: Image.Image) -> Image.Image:
    r, g, b, a = img.convert("RGBA").split()
    rgb = Image.merge("RGB", (r, g, b))
    rgb = ImageEnhance.Color(rgb).enhance(SATURATION)
    rgb = ImageEnhance.Brightness(rgb).enhance(BRIGHTNESS)
    r2, g2, b2 = rgb.split()
    return Image.merge("RGBA", (r2, g2, b2, a))


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)
    zip_path, scene_root = sys.argv[1], sys.argv[2]

    with tempfile.TemporaryDirectory() as tmp:
        with zipfile.ZipFile(zip_path) as z:
            names = [n for n in z.namelist() if n.endswith(tuple(src for src, _ in FILES))]
            z.extractall(tmp, members=names)
        # The zip's top-level folder name varies by version; find it.
        top = next(d for d in os.listdir(tmp) if os.path.isdir(os.path.join(tmp, d)))
        assets_root = os.path.join(tmp, top, "assets")

        for src_rel, out_rel in FILES:
            src_path = os.path.join(assets_root, src_rel)
            out_path = os.path.join(scene_root, out_rel)
            img = adjust(Image.open(src_path))
            img.save(out_path)
            print(f"wrote {out_path} ({img.size[0]}x{img.size[1]})")


if __name__ == "__main__":
    main()
