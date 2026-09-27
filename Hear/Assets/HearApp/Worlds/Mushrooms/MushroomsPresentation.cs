using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.Mushrooms
{
    /// <summary>
    /// World 8 - Mushrooms. Dev scratch world (human request 2026-09-27) built from a free
    /// low-poly mushroom set: 15 separate species FBX files, all sharing one tiny (1.3 KB) colour
    /// palette texture atlas rather than individual per-mushroom textures - typical of low-poly
    /// art, and confirmed via a throwaway InspectNewWorldModels diagnostic (each mushroom's own
    /// renderer already comes out at a sensible ~0.4-0.5 unit size and upright orientation with no
    /// extra scale/rotation correction needed, unlike every other model integrated so far this
    /// session). No ground/tree assets exist yet for this world, so the ground is a plain flat
    /// green primitive plane; a fuller forest floor can follow once those arrive.
    ///
    /// Turned into an actual reward loop (human feedback 2026-09-27: "Mohou... rust houby") - all
    /// slots are laid out up front (species/position/rotation/target scale) but start at zero
    /// scale; every correct detection grows the next one in with a pop-in animation, like a
    /// time-lapse of a mushroom pushing up out of the ground.
    /// </summary>
    public sealed class MushroomsPresentation : WorldPresentationBase
    {
        private static readonly string[] Species =
        {
            "amanita", "brown-cap_boletus", "cep", "chanterelle", "green_russule",
            "honey_mushroom", "milk_mushroom", "moss-fly_mushroom", "oily_mushroom",
            "orange-cap_boletus", "oyster_mushroom", "purple_russule", "saffron_milk_cap",
            "umbrella_mushroom", "yellow_mushroom",
        };

        private const int MushroomCount = 24;

        // Mushrooms scatter within this radius around the camera's fixed look target (0, 0.3, 0)
        // - a growing mushroom placed too far out could land outside the camera's FOV cone or
        // right at its border depending on the orbit's current angle (human report 2026-09-27:
        // "vyrostla... nekde na kraji nebo 'mimo vysec'"). At the orbit's distance from that look
        // target (~6.2, from OrbitRadius/OrbitHeight below), this radius keeps every mushroom's
        // angular offset well inside the ~27.5 deg vertical half-FOV regardless of where the
        // camera currently is. The ground plane itself (see GroundRadius below) is unrelated and
        // much larger, just to keep its own edge out of view.
        private const float GrowthRadius = 2.5f;

        private Camera _camera;
        private Transform _patchCenter;
        private readonly List<Transform> _mushroomSlots = new();
        private readonly List<Vector3> _mushroomTargetScales = new();
        private int _revealIndex;

        private void Awake()
        {
            BuildLighting();
            BuildGround();
            BuildMushrooms();
            BuildCamera();
        }

        // Shared between the camera's clear colour and fog, so the ground plane's edge (still
        // physically there - it's a finite primitive Plane) fades into an exact colour match
        // instead of being visible as a hard horizon line (human report 2026-09-27, said to apply
        // to every scene: "u ZADNE ze scen bych nechtel, aby byl videt ten zakladovy ctverec").
        private static readonly Color SkyColor = new(0.55f, 0.68f, 0.6f);

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.3f, 0.22f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 15f;

            var lightObject = new GameObject("MushroomsSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.85f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
        }

        // Vastly larger than the mushroom patch or the camera's orbit so its edge sits well past
        // where fog has already faded it to SkyColor - the plane never visibly ends.
        private const float GroundRadius = 40f;

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = Vector3.one * (GroundRadius * 2f / 10f);
            Destroy(ground.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = new Color(0.22f, 0.36f, 0.16f) };
            ground.GetComponent<Renderer>().sharedMaterial = material;

            _patchCenter = new GameObject("PatchCenter").transform;
            _patchCenter.SetParent(transform, false);
        }

        private void BuildMushrooms()
        {
            Texture2D atlas = Resources.Load<Texture2D>("Worlds/Mushrooms/Textures/MushroomAtlas");
            if (atlas != null) atlas.filterMode = FilterMode.Point;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var sharedMaterial = new Material(shader) { mainTexture = atlas };

            for (int i = 0; i < MushroomCount; i++)
            {
                string species = Species[i % Species.Length];
                var asset = Resources.Load<GameObject>($"Worlds/Mushrooms/Models/{species}");
                if (asset == null)
                {
                    Debug.LogError($"[Mushrooms] Could not load '{species}' from Resources.");
                    continue;
                }

                var instance = Instantiate(asset, transform);
                instance.name = $"{species}_{i}";

                Vector2 offset = Random.insideUnitCircle * GrowthRadius;
                instance.transform.localPosition += new Vector3(offset.x, 0f, offset.y);
                instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
                instance.transform.localScale *= Random.Range(0.85f, 1.3f);

                foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                    Destroy(importedCamera.gameObject);
                foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                    Destroy(importedLight.gameObject);

                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = sharedMaterial;

                Vector3 targetScale = instance.transform.localScale;
                instance.transform.localScale = Vector3.zero;
                _mushroomSlots.Add(instance.transform);
                _mushroomTargetScales.Add(targetScale);
            }
        }

        private IEnumerator GrowNextMushroomRoutine()
        {
            if (_revealIndex >= _mushroomSlots.Count)
            {
                RaiseListeningSafe();
                yield break;
            }

            var mushroom = _mushroomSlots[_revealIndex];
            Vector3 targetScale = _mushroomTargetScales[_revealIndex];
            _revealIndex++;

            const float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float eased = EaseOutBack(Mathf.Clamp01(elapsed / duration));
                if (mushroom != null) mushroom.localScale = targetScale * Mathf.Max(0f, eased);
                yield return null;
            }

            if (mushroom != null) mushroom.localScale = targetScale;
            RaiseListeningSafe();
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("MushroomsCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SkyColor;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
        }

        // Slow orbit over the patch, like a diorama turntable - reuses the same "keep it simple,
        // one gentle continuous motion" approach as the Planets/Earth worlds' spin.
        private const float OrbitRadius = 5.5f;
        private const float OrbitHeight = 3.2f;
        private const float OrbitDegreesPerSecond = 8f;

        private void Update()
        {
            if (_camera == null) return;

            float angle = Time.time * OrbitDegreesPerSecond * Mathf.Deg2Rad;
            var position = new Vector3(Mathf.Sin(angle) * OrbitRadius, OrbitHeight, Mathf.Cos(angle) * OrbitRadius);
            _camera.transform.position = position;
            _camera.transform.LookAt(new Vector3(0f, 0.3f, 0f), Vector3.up);
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

            StartCoroutine(GrowNextMushroomRoutine());
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[Mushrooms] Session complete: {result}");
        }
    }
}
