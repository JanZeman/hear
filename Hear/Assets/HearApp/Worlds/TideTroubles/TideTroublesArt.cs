using UnityEngine;

namespace HearApp.Worlds.TideTroubles
{
    /// <summary>
    /// Loads and slices the real production concept art shipped in
    /// hear-tide-troubles-handoff-v1.0 (see docs/v1.0-tide-troubles-handoff/), replacing the
    /// earlier greybox colored-quad placeholders. Per that handoff's README, the sheets are
    /// "source concept assets to be sliced by engineering" - there is no Sprite Editor pass
    /// available in this environment, so slicing rects are hand-authored here from visual
    /// inspection of each sheet instead of Editor-side grid/automatic slicing. Rects are kept
    /// generous (each sheet has ample transparent padding around every element) rather than
    /// pixel-exact, matching the prototype quality bar.
    ///
    /// All sprites are cut at runtime via <see cref="Sprite.Create"/> from plain imported
    /// Texture2D assets (textureType Default, same import settings as the rest of
    /// Assets/HearApp/Resources) rather than Editor-authored multi-sprite meta files, so no
    /// Unity Editor session is required to produce or update this slicing.
    /// </summary>
    public static class TideTroublesArt
    {
        private const string Root = "Worlds/TideTroubles/Scene";
        private const float Ppu = 300f;

        // Rects below are specified image-space (origin top-left, y grows downward) because
        // that is how the sheets read when viewed as images; MakeSprite converts to Unity's
        // bottom-left-origin sprite rect internally.

        public static Sprite Background => _background ??= LoadSingle("Backgrounds/harbor-background", 140f);
        public static Sprite DockFrame => _dockFrame ??= LoadSingle("Backgrounds/dock-frame", 140f);

        // --- Fish: 3x3 grid, rows = species (blue/gold/puffer), columns = pose (idle/jump/caught) ---
        public static Sprite FishIdle(int species) => FishGrid()[species * 3 + 0];
        public static Sprite FishJump(int species) => FishGrid()[species * 3 + 1];
        public static Sprite FishCaught(int species) => FishGrid()[species * 3 + 2];
        public const int FishSpeciesCount = 3;

        // --- Seagulls / dog: 4x2 grid, 8 flap/pose frames read left-to-right, top-to-bottom ---
        public static Sprite SeagullPose(int i) => SeagullGrid()[Mathf.Abs(i) % 8];
        public static Sprite DogPose(int i) => DogGrid()[Mathf.Abs(i) % 8];

        // --- Companion: curated picks, not a uniform grid - the sheet's poses aren't laid out on
        // a clean grid (unlike fish/seagull/dog) and a uniform 4x3 slice clipped the idle pose's
        // bottom edge (visible on-device as a "hole" - reported 2026-09-25).
        public static Sprite CompanionIdle => _companionIdle ??= MakeSprite(CompanionSheet, 0, 150, 320, 280);
        public static Sprite CompanionHappy => _companionHappy ??= MakeSprite(CompanionSheet, 680, 0, 400, 430);

        // --- Floating props: curated picks from the buoy/crate sheet ---
        public static Sprite FlagBuoy => _flagBuoy ??= MakeSprite(BuoySheet, 0, 0, 290, 217);
        public static Sprite LighthouseBuoy => _lighthouseBuoy ??= MakeSprite(BuoySheet, 0, 217, 290, 217);
        public static Sprite Barrel => _barrel ??= MakeSprite(BuoySheet, 0, 434, 362, 217);
        public static Sprite Crate => _crate ??= MakeSprite(BuoySheet, 0, 651, 362, 217);

        public static Sprite FloatingProp(int i)
        {
            switch (Mathf.Abs(i) % 4)
            {
                case 0: return FlagBuoy;
                case 1: return LighthouseBuoy;
                case 2: return Barrel;
                default: return Crate;
            }
        }

        // --- Launcher / net cannon: irregular layout, curated picks ---
        public static Sprite LauncherIdleLeft => _launcherIdleLeft ??= MakeSprite(LauncherSheet, 0, 0, 800, 440);
        public static Sprite LauncherIdleRight => _launcherIdleRight ??= MakeSprite(LauncherSheet, 870, 0, 578, 560);
        public static Sprite NetLoose => _netLoose ??= MakeSprite(LauncherSheet, 110, 790, 410, 296);

        // --- Splash / ripple: curated picks across the vibrant-water-splash sheet ---
        public static Sprite Splash(int i)
        {
            var arr = SplashPicks();
            return arr[Mathf.Abs(i) % arr.Length];
        }

        public static Sprite Ripple(int i)
        {
            var arr = RipplePicks();
            return arr[Mathf.Abs(i) % arr.Length];
        }

        // --- Target rings / glow / celebration: curated picks from golden-fishing-effects ---
        public static Sprite TargetRingGold => _ringGold ??= MakeSprite(EffectsSheet, 920, 0, 520, 460);
        public static Sprite CelebrationBurst => _celebrationBurst ??= MakeSprite(EffectsSheet, 1060, 730, 388, 356);

        // --- Texture loads (cached) ---
        private static Texture2D _fishTex, _seagullTex, _dogTex, _companionTex, _buoyTex, _launcherTex, _splashTex, _effectsTex;
        private static Texture2D FishSheet => _fishTex ??= Load("Sprites/fish-sheet");
        private static Texture2D SeagullSheet => _seagullTex ??= Load("Sprites/seagull-sheet");
        private static Texture2D DogSheet => _dogTex ??= Load("Sprites/dog-sheet");
        private static Texture2D CompanionSheet => _companionTex ??= Load("Sprites/companion-sheet");
        private static Texture2D BuoySheet => _buoyTex ??= Load("Sprites/buoy-sheet");
        private static Texture2D LauncherSheet => _launcherTex ??= Load("Sprites/launcher-sheet");
        private static Texture2D SplashSheet => _splashTex ??= Load("Sprites/splash-sheet");
        private static Texture2D EffectsSheet => _effectsTex ??= Load("Sprites/effects-sheet");

        private static Texture2D Load(string relativePath) => Resources.Load<Texture2D>($"{Root}/{relativePath}");

        // --- Grid caches ---
        private static Sprite[] _fishGrid, _seagullGrid, _dogGrid, _splashPicks, _ripplePicks;
        private static Sprite _background, _dockFrame;
        private static Sprite _flagBuoy, _lighthouseBuoy, _barrel, _crate;
        private static Sprite _launcherIdleLeft, _launcherIdleRight, _netLoose;
        private static Sprite _ringGold, _celebrationBurst;
        private static Sprite _companionIdle, _companionHappy;

        // Fish/seagull/dog are curated picks, not a uniform grid: the sheets lay each pose out at
        // its own size and position (not evenly spaced), so a uniform 482x362 / 362x543 slice cut
        // into fins, wings and tails on several poses (reported 2026-09-26: "u hodně z nich vidím
        // špatný ořez"). Rects below were measured from each sheet's actual non-transparent pixel
        // bounds (per pose, plus a few px of padding) rather than assuming equal cell sizes.
        private static Sprite[] FishGrid() => _fishGrid ??= new[]
        {
            MakeSprite(FishSheet, 77, 83, 409, 229),
            MakeSprite(FishSheet, 631, 21, 246, 355),
            MakeSprite(FishSheet, 1051, 124, 373, 251),
            MakeSprite(FishSheet, 118, 430, 396, 247),
            MakeSprite(FishSheet, 601, 393, 293, 328),
            MakeSprite(FishSheet, 1055, 448, 364, 271),
            MakeSprite(FishSheet, 154, 738, 319, 262),
            MakeSprite(FishSheet, 587, 723, 337, 337),
            MakeSprite(FishSheet, 1046, 805, 378, 253),
        };

        private static Sprite[] SeagullGrid() => _seagullGrid ??= new[]
        {
            MakeSprite(SeagullSheet, 34, 76, 304, 391),
            MakeSprite(SeagullSheet, 341, 218, 382, 241),
            MakeSprite(SeagullSheet, 731, 244, 292, 261),
            MakeSprite(SeagullSheet, 1014, 224, 425, 221),
            MakeSprite(SeagullSheet, 24, 554, 330, 357),
            MakeSprite(SeagullSheet, 355, 659, 388, 246),
            MakeSprite(SeagullSheet, 746, 673, 294, 290),
            MakeSprite(SeagullSheet, 1021, 667, 403, 232),
        };

        private static Sprite[] DogGrid() => _dogGrid ??= new[]
        {
            MakeSprite(DogSheet, 32, 86, 333, 428),
            MakeSprite(DogSheet, 384, 88, 397, 428),
            MakeSprite(DogSheet, 769, 149, 378, 379),
            MakeSprite(DogSheet, 1153, 88, 269, 429),
            MakeSprite(DogSheet, 32, 621, 437, 378),
            MakeSprite(DogSheet, 465, 529, 338, 481),
            MakeSprite(DogSheet, 803, 607, 294, 388),
            MakeSprite(DogSheet, 1138, 584, 281, 428),
        };

        private static Sprite[] SplashPicks() => _splashPicks ??= new[]
        {
            MakeSprite(SplashSheet, 207, 0, 207, 300),
            MakeSprite(SplashSheet, 621, 0, 207, 300),
            MakeSprite(SplashSheet, 1035, 0, 207, 300),
        };

        private static Sprite[] RipplePicks() => _ripplePicks ??= new[]
        {
            MakeSprite(SplashSheet, 241, 480, 241, 160),
            MakeSprite(SplashSheet, 723, 480, 241, 160),
        };

        private static Sprite LoadSingle(string relativePath, float ppu)
        {
            var tex = Load(relativePath);
            return tex == null ? null : Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        }

        // All sheets (fish/seagull/dog/companion/buoy/launcher/splash/effects) ship at this
        // native size; rects below are authored against it. Import settings disable
        // power-of-two rescaling (see the sheets' .meta nPOTScale: 0) so textures should load at
        // exactly this size - but rects are still rescaled proportionally against the texture's
        // actual size as a defensive fallback, since a texture reimported at a different size
        // (e.g. nPOTScale flipped back on, or a platform max-size override) previously crashed
        // TideTroublesPresentation.Awake outright (see docs/v1.0-tide-troubles-handoff).
        private const int NativeSheetSize = 1448;
        private const int NativeSheetHeight = 1086;

        /// <summary>xImg/yImgTop are image-space (top-left origin) in native-sheet pixels;
        /// converted here to Unity's bottom-left sprite-rect space in the texture's actual
        /// (possibly rescaled) pixel space.</summary>
        private static Sprite MakeSprite(Texture2D tex, int xImg, int yImgTop, int w, int h)
        {
            if (tex == null) return null;
            float scaleX = tex.width / (float)NativeSheetSize;
            float scaleY = tex.height / (float)NativeSheetHeight;
            int rx = Mathf.RoundToInt(xImg * scaleX);
            int ry = Mathf.RoundToInt(yImgTop * scaleY);
            int rw = Mathf.Max(1, Mathf.RoundToInt(w * scaleX));
            int rh = Mathf.Max(1, Mathf.RoundToInt(h * scaleY));
            int unityY = Mathf.Max(0, tex.height - (ry + rh));
            var rect = new Rect(rx, unityY, rw, rh);
            return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Ppu);
        }
    }
}
