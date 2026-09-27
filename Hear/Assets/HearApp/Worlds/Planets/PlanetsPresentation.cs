using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.Planets
{
    /// <summary>
    /// World 6 - Planets. Dev scratch world (human request 2026-09-27: try out the free CGTrader
    /// "Photorealistic Solar System" pack). The source OBJ lays all 11 bodies out on two rows like
    /// a display shelf (measured via a throwaway InspectNewWorldModels diagnostic) with near-
    /// identical per-body mesh radii regardless of the real body's size, so BuildEnvironment
    /// re-parents each named child to a single evenly spaced line in solar-system order and
    /// applies a tasteful (not physically accurate) relative scale. The camera then reuses the
    /// VikingBoat world's flythrough pattern - glide along the row at close range so each body
    /// reads clearly as it passes.
    ///
    /// Turned into an actual reward loop (human feedback 2026-09-27: "planety jsou trochu
    /// chaoticke... Mohou se... objevovat planety") - only the Sun is visible at first; every
    /// correct detection reveals the next body outward in solar-system order with a pop-in grow
    /// animation, which doubles as the fix for "chaotic": far fewer objects are visible at once
    /// early in a session.
    /// </summary>
    public sealed class PlanetsPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private float _startTime;
        private readonly List<Transform> _spinners = new();

        private void Awake()
        {
            _startTime = Time.time;
            BuildLighting();
            BuildEnvironment();
            BuildCamera();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.ambientIntensity = 1f;

            var lightObject = new GameObject("PlanetsSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.92f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("PlanetsCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 300f;
            _camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
        }

        // Row layout: each body placed along local Z in solar-system order, spacing derived from
        // the (tasteful, non-physical) per-body scale below so neighbours never overlap. Moon
        // rides right next to Earth rather than taking its own row slot.
        private static readonly (string childName, string textureName, float scale, float z)[] Bodies =
        {
            ("Sun_Sphere.003_Sun", "Sun", 1.8f, 0f),
            ("Mercury_Sphere.009_Mercury", "Mercury", 0.55f, 3.1f),
            ("Venus_Sphere.010_Venus", "Venus", 0.95f, 5.4f),
            ("Earth_Sphere.002_Earth", "Earth", 1f, 7.7f),
            ("Mars_Sphere.004_Mars", "Mars", 0.8f, 9.9f),
            ("Jupiter_Sphere.005_Jupiter", "Jupiter", 1.6f, 13f),
            ("Saturn_Sphere.006_Saturn", "Saturn", 1.3f, 17.2f),
            ("Ring_Circle.001_Saturn's_ring", "SaturnRing", 1.3f, 17.2f),
            ("Uranus_Sphere.007_Uranus", "Uranus", 1.05f, 21.6f),
            ("Neptun_Sphere.008_Neptun", "Neptune", 1f, 24.1f),
        };

        private const string MoonChildName = "Moon_Sphere_Moon";
        private const string MoonTextureName = "Moon";
        private const float MoonScale = 0.4f;
        private static readonly Vector3 MoonOffsetFromEarth = new(1.1f, 0.35f, -0.6f);

        private const float RowHalfSpan = 13.5f;
        private const float RowCenterZ = 12.05f;

        private readonly Dictionary<string, Material> _materialCache = new();
        private readonly Dictionary<string, Transform> _bodiesByName = new();

        // Sun is visible from the start; everything else is revealed one step at a time on a
        // correct detection, outward in solar-system order. Earth reveals with its Moon, Saturn
        // with its ring.
        private static readonly string[][] RevealSteps =
        {
            new[] { "Mercury" },
            new[] { "Venus" },
            new[] { "Earth", "Moon" },
            new[] { "Mars" },
            new[] { "Jupiter" },
            new[] { "Saturn", "SaturnRing" },
            new[] { "Uranus" },
            new[] { "Neptune" },
        };

        private int _revealIndex;

        private void BuildEnvironment()
        {
            var asset = Resources.Load<GameObject>("Worlds/Planets/Models/PlanetsOBJ");
            if (asset == null)
            {
                Debug.LogError("[Planets] Could not load PlanetsOBJ model from Resources.");
                return;
            }

            var instance = Instantiate(asset, transform);
            foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                Destroy(importedCamera.gameObject);
            foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                Destroy(importedLight.gameObject);

            Vector3 earthPosition = Vector3.zero;
            foreach (var body in Bodies)
            {
                var child = instance.transform.Find(body.childName);
                if (child == null)
                {
                    Debug.LogError($"[Planets] Missing expected child '{body.childName}'.");
                    continue;
                }

                child.SetParent(transform, false);
                child.localPosition = new Vector3(0f, 0f, body.z);
                child.localScale = Vector3.one * body.scale;
                ApplyTexture(child, body.textureName);
                _spinners.Add(child);
                _bodiesByName[body.textureName] = child;

                if (body.childName == "Earth_Sphere.002_Earth") earthPosition = child.localPosition;
            }

            var moon = instance.transform.Find(MoonChildName);
            if (moon != null)
            {
                moon.SetParent(transform, false);
                moon.localPosition = earthPosition + MoonOffsetFromEarth;
                moon.localScale = Vector3.one * MoonScale;
                ApplyTexture(moon, MoonTextureName);
                _spinners.Add(moon);
                _bodiesByName["Moon"] = moon;
            }

            Destroy(instance);

            // Hide everything except the Sun until it's earned.
            foreach (var pair in _bodiesByName)
                pair.Value.gameObject.SetActive(false);
            if (_bodiesByName.TryGetValue("Sun", out var sun)) sun.gameObject.SetActive(true);
        }

        private IEnumerator RevealNextBodyRoutine()
        {
            if (_revealIndex >= RevealSteps.Length)
            {
                RaiseListeningSafe();
                yield break;
            }

            var names = RevealSteps[_revealIndex++];
            var reveals = new List<(Transform t, Vector3 targetScale)>();
            foreach (var name in names)
            {
                if (!_bodiesByName.TryGetValue(name, out var t)) continue;
                Vector3 targetScale = t.localScale;
                t.localScale = Vector3.zero;
                t.gameObject.SetActive(true);
                reveals.Add((t, targetScale));
            }

            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float eased = EaseOutBack(Mathf.Clamp01(elapsed / duration));
                foreach (var (t, targetScale) in reveals)
                    if (t != null) t.localScale = targetScale * Mathf.Max(0f, eased);
                yield return null;
            }

            foreach (var (t, targetScale) in reveals)
                if (t != null) t.localScale = targetScale;

            RaiseListeningSafe();
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void ApplyTexture(Transform part, string textureName)
        {
            var renderer = part.GetComponent<Renderer>();
            if (renderer == null) return;

            if (!_materialCache.TryGetValue(textureName, out var material))
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                var texture = Resources.Load<Texture2D>($"Worlds/Planets/Textures/{textureName}");
                material.mainTexture = texture;

                if (textureName == "Sun")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetTexture("_EmissionMap", texture);
                    material.SetColor("_EmissionColor", Color.white);
                }

                // The ring alpha texture was tried as a transparent material first, but the
                // "gap" alpha detail rendered as solid opaque regardless of the URP Lit
                // transparency setup (same unresolved shell-transparency issue hit in the Earth
                // world - not worth chasing for this pass). The ring mesh ("Ring_Circle") is
                // already a proper annulus with the centre hole built into its geometry, so a
                // plain opaque material still reads correctly as a ring, just without the fine
                // banding gaps.

                _materialCache[textureName] = material;
            }

            renderer.sharedMaterial = material;
        }

        // Flythrough: same shape as the VikingBoat world's proven camera pattern - glide along the
        // row's long axis at a fixed lateral offset, looking perpendicular at whatever is
        // alongside. Each body also spins slowly on its own axis for a bit of life.
        private const float FlythroughSideOffset = 3.6f;
        private const float FlythroughDuration = 20f;
        private const float SpinDegreesPerSecond = 10f;

        private void Update()
        {
            foreach (var spinner in _spinners)
                if (spinner != null) spinner.Rotate(Vector3.up, SpinDegreesPerSecond * Time.deltaTime, Space.World);

            if (_camera == null) return;

            float elapsed = Time.time - _startTime;
            float normalized = Mathf.PingPong(elapsed, FlythroughDuration) / FlythroughDuration;
            float z = Mathf.Lerp(RowCenterZ - RowHalfSpan, RowCenterZ + RowHalfSpan, normalized);

            _camera.transform.position = new Vector3(FlythroughSideOffset, 0f, z);
            _camera.transform.LookAt(new Vector3(0f, 0f, z), Vector3.up);
        }

        public override void Initialize(WorldContext context)
        {
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome != TrialOutcome.CorrectDetection)
            {
                RaiseListeningSafe();
                return;
            }

            StartCoroutine(RevealNextBodyRoutine());
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[Planets] Session complete: {result}");
        }
    }
}
