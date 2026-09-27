using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.Experiment
{
    /// <summary>
    /// World 4 - Experiment. A scratch space for trying out free downloaded 3D environment
    /// assets quickly, separate from the three real worlds, per human request 2026-09-26:
    /// "Zkusme experimentovat s ruznymi svety." Deliberately minimal - it does not yet play a
    /// real hearing-trial gameplay loop, it just proves out an environment asset in isolation.
    ///
    /// Second asset under test: a free "Castle Low Poly" model (the first attempt, a lighthouse,
    /// shipped with no usable texture/color data at all - see git history). This one comes with a
    /// real Textures.zip (20 named PNG/JPG maps matching the FBX's 20 material names one-to-one),
    /// wired on in <see cref="ApplyTextures"/>.
    ///
    /// Known concern, not yet addressed: the FBX imports as 7233 separate MeshFilters. That is a
    /// lot of draw calls for a mobile target - fine for this local macOS look-and-feel pass, but
    /// worth measuring/optimizing (static batching, mesh merging) before this becomes more than an
    /// experiment.
    /// </summary>
    public sealed class ExperimentPresentation : WorldPresentationBase
    {
        private Camera _camera;

        private void Awake()
        {
            BuildLighting();
            BuildEnvironment();
            BuildCamera();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.38f);
            RenderSettings.ambientIntensity = 1f;

            var lightObject = new GameObject("ExperimentSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.9f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("ExperimentCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.55f, 0.68f, 0.78f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 300f;
            _camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();

            // A wide establishing shot lost the castle as a tiny speck in a vast mountain range
            // (human feedback 2026-09-26: "at ho vidime" - close, low, three-quarter hero shot like
            // a real product render). BuildEnvironment computes _castleBounds from renderers using
            // building-specific materials only (Brick/Wood/Roof/Door/...), excluding the
            // Mountain/Water/Landscape-Low-Poly materials that cover the huge surrounding terrain -
            // that isolates the castle+village cluster from the rest of the 600-unit-deep map.
            Vector3 center = _castleBounds.center;
            float radius = _castleBounds.extents.magnitude;
            cameraObject.transform.position = center + new Vector3(0f, radius * 0.55f, -radius * 1.3f);
            cameraObject.transform.LookAt(center + Vector3.up * radius * 0.25f, Vector3.up);
        }

        // Combined mesh bounds measured via a one-off Editor diagnostic: center
        // (-12.42, -3.90, 5.92), size (376.15, 91.68, 596.83) - recentered to local origin and
        // scaled down to a manageable footprint.
        private static readonly Vector3 SourceBoundsCenter = new(-12.42f, -3.9f, 5.92f);
        private const float Scale = 0.05f;

        private void BuildEnvironment()
        {
            var asset = Resources.Load<GameObject>("Worlds/Experiment/Models/Castle");
            if (asset == null)
            {
                Debug.LogError("[Experiment] Could not load Castle model from Resources.");
                return;
            }

            var pivot = new GameObject("Castle").transform;
            pivot.SetParent(transform, false);
            pivot.localScale = Vector3.one * Scale;

            var instance = Instantiate(asset);
            instance.transform.SetParent(pivot, false);
            instance.transform.localPosition = -SourceBoundsCenter;

            // The FBX ships with the source 3D software's own scene camera AND light baked in as
            // child objects (the same pattern found on the earlier lighthouse attempt: an embedded
            // camera silently composited over our own every frame with Unity's default sky,
            // making every camera-setting change look like it had no effect at all). Strip both so
            // only our own camera/light remain.
            foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                Destroy(importedCamera.gameObject);
            foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                Destroy(importedLight.gameObject);

            ApplyTextures(instance);
        }

        // The FBX's 20 material names map directly onto Textures.zip's file names (copied into
        // Assets/HearApp/Resources/Worlds/Experiment/Textures/, spaces stripped from filenames).
        // A few materials (Gold trim, the stone Balcony, the Lock hardware, Water) have no matching
        // texture in the pack at all - those fall back to a flat, roughly-plausible color instead.
        private static readonly (string materialName, string textureName)[] TexturedMaterials =
        {
            ("Brick", "Brick"),
            ("Wood", "Wood"),
            ("Wood.001", "Wood"),
            ("Roof", "RoofShingles"),
            ("Roof Shingles", "RoofShingles"),
            ("Flag", "Flag"),
            ("Top Flag", "FlagMain"),
            ("Rock", "Rock"),
            ("Rock.001", "Rock"),
            ("Log", "Log"),
            ("Railing", "Railing"),
            ("Chimney", "Chimney"),
            ("Door", "Door"),
            ("Mountain", "Mountain"),
            ("Landscape Low-Poly", "LandscapeLowPolyTerrain"),
            ("Leaf", "Leaf"),
        };

        private static readonly (string materialName, Color color)[] FlatColorMaterials =
        {
            ("Gold", new Color(0.83f, 0.68f, 0.21f)),
            ("Balcony", new Color(0.55f, 0.53f, 0.5f)),
            ("Lock", new Color(0.25f, 0.24f, 0.23f)),
            ("Water", new Color(0.2f, 0.4f, 0.55f)),
        };

        // Materials that only ever belong to the castle/village structures themselves, never the
        // surrounding terrain - used to isolate just that cluster's bounds for camera framing (see
        // BuildCamera). Deliberately excludes Rock/Rock.001/Leaf/Log, which read as decorative
        // scatter across the whole huge landscape rather than being castle-specific.
        private static readonly HashSet<string> CastleMaterialNames = new()
        {
            "Brick", "Wood", "Wood.001", "Roof", "Roof Shingles", "Flag", "Top Flag",
            "Chimney", "Door", "Gold", "Balcony", "Lock", "Railing",
        };

        private readonly Dictionary<string, Material> _materialCache = new();
        private Bounds _castleBounds;

        private void ApplyTextures(GameObject instance)
        {
            bool haveCastleBounds = false;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var original = renderer.sharedMaterials;
                var replaced = new Material[original.Length];
                bool isCastlePart = false;
                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;
                    if (CastleMaterialNames.Contains(original[i].name)) isCastlePart = true;
                    replaced[i] = BuildMaterialFor(original[i].name);
                }
                renderer.sharedMaterials = replaced;

                if (!isCastlePart) continue;
                if (!haveCastleBounds) { _castleBounds = renderer.bounds; haveCastleBounds = true; }
                else _castleBounds.Encapsulate(renderer.bounds);
            }
        }

        private Material BuildMaterialFor(string materialName)
        {
            if (_materialCache.TryGetValue(materialName, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);

            var texturedEntry = System.Array.Find(TexturedMaterials, e => e.materialName == materialName);
            if (texturedEntry.textureName != null)
            {
                material.mainTexture = Resources.Load<Texture2D>($"Worlds/Experiment/Textures/{texturedEntry.textureName}");
            }
            else
            {
                var flatEntry = System.Array.Find(FlatColorMaterials, e => e.materialName == materialName);
                material.color = flatEntry.materialName != null ? flatEntry.color : Color.gray;
            }

            _materialCache[materialName] = material;
            return material;
        }

        public override void Initialize(WorldContext context)
        {
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            // No real gameplay loop yet - this world only exists to look at an environment asset.
            RaiseListeningSafe();
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[Experiment] Session complete: {result}");
        }
    }
}
