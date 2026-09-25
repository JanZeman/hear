using System.Collections.Generic;
using UnityEngine;

namespace HearApp.Core.Shell
{
    /// <summary>
    /// Loads the real prototype art shipped in the hear-ui-assets-v0.3 package (copied into
    /// Assets/HearApp/Resources) instead of approximating it with placeholder colors. Per that
    /// package's own rules, these are approved prototype exports - use them directly rather than
    /// re-deriving anything from concept boards.
    /// </summary>
    public static class WorldArt
    {
        public readonly struct WorldTextures
        {
            public readonly Texture2D Wide169;
            public readonly Texture2D Card43;
            public readonly Texture2D Square11;
            public readonly Texture2D Portrait916;
            public readonly Texture2D Ambient169;

            public WorldTextures(Texture2D wide169, Texture2D card43, Texture2D square11, Texture2D portrait916, Texture2D ambient169)
            {
                Wide169 = wide169;
                Card43 = card43;
                Square11 = square11;
                Portrait916 = portrait916;
                Ambient169 = ambient169;
            }
        }

        /// <summary>Normalized (0..1) anchor within the hero rect where the Companion's feet/base
        /// should sit, plus its height as a fraction of hero height. These are approximate manual
        /// placements (see README known limitations) - the supplied world art has no
        /// baked-in landing surface reserved for the Companion, unlike the original concept board.</summary>
        public readonly struct CompanionPlacement
        {
            public readonly float AnchorX;
            public readonly float AnchorY;
            public readonly float HeightFractionOfHero;

            public CompanionPlacement(float anchorX, float anchorY, float heightFractionOfHero)
            {
                AnchorX = anchorX;
                AnchorY = anchorY;
                HeightFractionOfHero = heightFractionOfHero;
            }
        }

        // Keyed by WorldRegistry.WorldEntry.Id rather than array index - a previous index-based
        // parallel array here silently drifted out of sync with WorldRegistry's own order (river
        // art was shown under the "Tide Troubles" title), so lookups are now by stable id string.
        private static readonly Dictionary<string, string> ResourceFolders = new()
        {
            ["tide-troubles"] = "Worlds/TideTroubles",
            ["paper-garden"] = "Worlds/PaperGarden",
            ["river-journey"] = "Worlds/RiverJourney",
        };

        private static readonly Dictionary<string, CompanionPlacement> CompanionPlacements = new()
        {
            ["tide-troubles"] = new CompanionPlacement(0.72f, 0.86f, 0.20f),   // dockside, right foreground
            ["paper-garden"] = new CompanionPlacement(0.24f, 0.84f, 0.17f),    // quiet foreground corner, out of the stage action
            ["river-journey"] = new CompanionPlacement(0.70f, 0.68f, 0.16f),  // near the shoreline rocks visible mid-right of the scene
        };

        public static WorldTextures GetWorldTextures(string worldId)
        {
            string folder = ResourceFolders.TryGetValue(worldId, out var f) ? f : "Worlds/TideTroubles";
            return new WorldTextures(
                Resources.Load<Texture2D>($"{folder}/world-16x9"),
                Resources.Load<Texture2D>($"{folder}/world-4x3"),
                Resources.Load<Texture2D>($"{folder}/world-1x1"),
                Resources.Load<Texture2D>($"{folder}/world-9x16"),
                Resources.Load<Texture2D>($"{folder}/ambient-bg-16x9"));
        }

        public static CompanionPlacement GetCompanionPlacement(string worldId) =>
            CompanionPlacements.TryGetValue(worldId, out var p) ? p : CompanionPlacements["tide-troubles"];

        public static Texture2D CompanionNeutral => Resources.Load<Texture2D>("Companion/companion-neutral-512");
        public static Texture2D CompanionCurious => Resources.Load<Texture2D>("Companion/companion-curious-512");
        public static Texture2D CompanionHappy => Resources.Load<Texture2D>("Companion/companion-happy-512");

        // hear-logo-handoff-v3.1 (canonical, supersedes v2.2/v3.0/all earlier) - see
        // Art/Reference/brand/v3.1/. Do not redraw/reinterpret; only the raster export path may
        // change (e.g. a resolution swap), per that handoff's explicit rules. Splash uses the
        // with-claim variant; Home uses no-claim (it draws "Sound opens worlds" itself as live
        // text - see ShellUIController); mark-only is for compact/quiet contexts (nav rail).
        public static Texture2D LogoWithClaim => Resources.Load<Texture2D>("Brand/hear-logo-with-claim-on-light-1024");
        public static Texture2D MarkOnly => Resources.Load<Texture2D>("Brand/hear-mark-only-on-light-512");
        public static Texture2D LogoOnDark => Resources.Load<Texture2D>("Brand/hear-logo-no-claim-on-dark-1024");

        // --- v1.0 Home handoff: production Companion + separate shadow (still current) ---
        public static Texture2D CompanionOnDarkNeutralLeft => Resources.Load<Texture2D>("Companion/OnDark/companion-neutral-left");
        public static Texture2D CompanionOnDarkNeutralRight => Resources.Load<Texture2D>("Companion/OnDark/companion-neutral-right");
        public static Texture2D CompanionShadow => Resources.Load<Texture2D>("Companion/Shadows/companion-shadow-512");

        public enum HomeBgAspect { Portrait, Square, Landscape, Ultrawide }

        /// <summary>
        /// The Home screen's full-bleed background source for the active world (pre-treatment -
        /// see <see cref="GetTreatedAmbient"/>). River Journey ships dedicated Home background
        /// variants per aspect in this handoff; the other two worlds do not, so they fall back to
        /// their own world-16x9/master-clean art - a documented, honest placeholder gap, not a
        /// redraw.
        /// </summary>
        public static Texture2D GetHomeBackground(string worldId, HomeBgAspect aspect)
        {
            if (worldId == "river-journey")
            {
                string suffix = aspect switch
                {
                    HomeBgAspect.Portrait => "home-bg-portrait",
                    HomeBgAspect.Square => "home-bg-square",
                    HomeBgAspect.Ultrawide => "home-bg-ultrawide",
                    _ => "home-bg-landscape"
                };
                var tex = Resources.Load<Texture2D>($"Worlds/RiverJourney/{suffix}");
                if (tex != null) return tex;
            }

            var textures = GetWorldTextures(worldId);
            return aspect == HomeBgAspect.Portrait && textures.Portrait916 != null ? textures.Portrait916 : textures.Wide169;
        }

        private static readonly Dictionary<Texture2D, RenderTexture> AmbientTreatedCache = new();
        private static Material _ambientTreatmentMaterial;

        /// <summary>
        /// Blurred/darkened/desaturated version of an ambient Home background, so the carousel
        /// cards and UI read as the sharp interactive foreground against a hazier backdrop -
        /// deliberately reversing the earlier v1.0 handoff's "sharp active-world background,
        /// never generic blur" rule (docs/v1.0-home-handoff/), per later designer feedback
        /// 2026-09-25 ("uzivatel si nemusi uvedomit, ze tam je carousel"). Computed once per
        /// source texture and cached (never per-frame): a cheap downsample blit first (the real
        /// softness), then Shaders/AmbientTreatment.shader (a Resources-folder shader, so it is
        /// never build-stripped the way Shader.Find lookups can be - see roadmap 001's notes on
        /// the URP particle shader bug) does a small multi-tap blur plus the darken/desaturate
        /// lerp in one pass. Falls back to the untreated source if the shader can't be loaded,
        /// rather than showing nothing.
        /// </summary>
        public static Texture GetTreatedAmbient(Texture2D source)
        {
            if (source == null) return null;
            if (AmbientTreatedCache.TryGetValue(source, out var cached) && cached != null) return cached;

            if (_ambientTreatmentMaterial == null)
            {
                var shader = Resources.Load<Shader>("Shaders/AmbientTreatment");
                if (shader == null)
                {
                    Debug.LogWarning("[WorldArt] AmbientTreatment shader not found; showing the untreated background.");
                    return source;
                }
                _ambientTreatmentMaterial = new Material(shader);
            }

            int downW = Mathf.Max(8, source.width / 6);
            int downH = Mathf.Max(8, source.height / 6);
            var small = RenderTexture.GetTemporary(downW, downH, 0, RenderTextureFormat.ARGB32);
            small.filterMode = FilterMode.Bilinear;
            Graphics.Blit(source, small);

            var result = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear
            };
            result.Create();
            Graphics.Blit(small, result, _ambientTreatmentMaterial);
            RenderTexture.ReleaseTemporary(small);

            AmbientTreatedCache[source] = result;
            return result;
        }

        public static Texture2D Icon(string name) => Resources.Load<Texture2D>($"Icons/{name}");
    }
}
