using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.Earth
{
    /// <summary>
    /// World 7 - Earth. Dev scratch world (human request 2026-09-27) built from a dedicated
    /// high-detail single-planet FBX rather than the multi-body Planets pack. The source model
    /// (measured via a throwaway InspectNewWorldModels diagnostic) already ships as the classic
    /// three-shell technique: "surface" (opaque land/ocean), "cloud" (a slightly larger shell,
    /// meant to be semi-transparent) and "atmo" (a larger shell again, meant as an atmosphere rim)
    /// - so no extra geometry is needed, only per-shell materials. The source textures were
    /// 8K-21K TIFFs (tens of MB each); the color map was downscaled to 2K/jpg and the clouds map
    /// had its RGB luminance copied into an alpha channel (via ImageMagick, since the source has
    /// no real alpha) so it can render as a translucent shell over the surface.
    ///
    /// Turned into an actual reward loop (human feedback 2026-09-27: "Mohou se rozsvecovat
    /// svetadily") - a small glowing marker sits on the globe at each continent's approximate
    /// centre (lat/lon converted to a point on the surface mesh, placed in the same native/
    /// unscaled space the globe itself is recentred in), all hidden at first. Every correct
    /// detection lights up the next one with a pop-in animation. Placement uses a standard
    /// equirectangular lat/lon convention but the source mesh's own UV alignment/rotation wasn't
    /// individually verified against it, so treat "which continent" as approximate, not exact.
    /// </summary>
    public sealed class EarthPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private Transform _globePivot;

        private void Awake()
        {
            BuildLighting();
            BuildEnvironment();
            BuildCamera();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.06f, 0.09f);
            RenderSettings.ambientIntensity = 1f;

            var lightObject = new GameObject("EarthSun");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(25f, -50f, 0f);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.98f, 0.94f);
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("EarthCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
            cameraObject.transform.position = new Vector3(0f, 0.25f, -4.2f);
            cameraObject.transform.LookAt(Vector3.zero, Vector3.up);
        }

        // Source combined bounds (all 3 shells) measured center (0, 489.54, 0), largest shell
        // ("cloud") size ~2042 - the model is authored at a huge arbitrary scale, so it's rescaled
        // down to a ~3-unit-diameter globe, consistent with other worlds' focal-object sizing.
        private static readonly Vector3 SourceBoundsCenter = new(0f, 489.54f, 0f);
        private const float NativeDiameter = 2042.10f;
        private const float TargetDiameter = 3f;
        private const float Scale = TargetDiameter / NativeDiameter;

        private void BuildEnvironment()
        {
            var asset = Resources.Load<GameObject>("Worlds/Earth/Models/Earth");
            if (asset == null)
            {
                Debug.LogError("[Earth] Could not load Earth model from Resources.");
                return;
            }

            _globePivot = new GameObject("EarthGlobe").transform;
            _globePivot.SetParent(transform, false);
            _globePivot.localScale = Vector3.one * Scale;

            var instance = Instantiate(asset);
            instance.transform.SetParent(_globePivot, false);
            instance.transform.localPosition = -SourceBoundsCenter;

            foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                Destroy(importedCamera.gameObject);
            foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                Destroy(importedLight.gameObject);

            var shader = Shader.Find("Universal Render Pipeline/Lit");

            var surfaceMaterial = new Material(shader);
            surfaceMaterial.mainTexture = Resources.Load<Texture2D>("Worlds/Earth/Textures/EarthColor");

            // The "cloud" and "atmo" shells are meant to render as translucent layers over
            // "surface" (the classic multi-shell planet technique), but both came out rendering
            // as solid opaque colour despite the correct URP Lit transparency setup (_Surface,
            // blend keywords, render queue all verified) - likely a normals/winding issue on
            // those specific shells rather than a material problem, since disabling them
            // entirely (below) reveals a perfectly correct "surface" texture underneath.
            // Diagnosing the shell geometry further wasn't worth it for this pass, so they're
            // switched off for now; "surface" alone already looks correct and is what ships here.
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                switch (renderer.name)
                {
                    case "surface": renderer.sharedMaterial = surfaceMaterial; break;
                    case "cloud":
                    case "atmo":
                        renderer.gameObject.SetActive(false);
                        break;
                    default: Debug.LogWarning($"[Earth] Unrecognized shell '{renderer.name}', leaving its imported material as-is."); break;
                }
            }

            BuildContinentMarkers();
        }

        // Approximate centroids, standard lat/lon in degrees. Revealed in this order.
        private static readonly (string name, float lat, float lon)[] Continents =
        {
            ("Europe", 50f, 15f),
            ("Africa", 5f, 20f),
            ("Asia", 35f, 100f),
            ("NorthAmerica", 45f, -100f),
            ("SouthAmerica", -15f, -60f),
            ("Australia", -25f, 135f),
            ("Antarctica", -80f, 0f),
        };

        // Half the average of the "surface" shell's measured bounds (1689.93, 1518.20, 1474.06) -
        // markers sit on the sphere at this native-space radius, inside the same unscaled pivot
        // space the globe mesh is recentred in, so EarthGlobe's own Scale shrinks them correctly.
        private const float SurfaceNativeRadius = 780f;
        private const float MarkerNativeDiameter = 60f;

        private readonly List<Transform> _continentMarkers = new();
        private int _revealIndex;

        private void BuildContinentMarkers()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var markerMaterial = new Material(shader) { color = Color.yellow };
            markerMaterial.EnableKeyword("_EMISSION");
            markerMaterial.SetColor("_EmissionColor", Color.yellow * 2f);

            foreach (var continent in Continents)
            {
                float latRad = continent.lat * Mathf.Deg2Rad;
                float lonRad = continent.lon * Mathf.Deg2Rad;
                var direction = new Vector3(
                    Mathf.Cos(latRad) * Mathf.Sin(lonRad),
                    Mathf.Sin(latRad),
                    Mathf.Cos(latRad) * Mathf.Cos(lonRad));

                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = $"ContinentMarker_{continent.name}";
                marker.transform.SetParent(_globePivot, false);
                marker.transform.localPosition = direction * SurfaceNativeRadius;
                marker.transform.localScale = Vector3.one * MarkerNativeDiameter;
                Destroy(marker.GetComponent<Collider>());
                marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                marker.SetActive(false);

                _continentMarkers.Add(marker.transform);
            }
        }

        private IEnumerator RevealNextContinentRoutine()
        {
            if (_revealIndex >= _continentMarkers.Count)
            {
                RaiseListeningSafe();
                yield break;
            }

            var marker = _continentMarkers[_revealIndex++];
            Vector3 targetScale = marker.localScale;
            marker.localScale = Vector3.zero;
            marker.gameObject.SetActive(true);

            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float eased = EaseOutBack(Mathf.Clamp01(elapsed / duration));
                if (marker != null) marker.localScale = targetScale * Mathf.Max(0f, eased);
                yield return null;
            }

            if (marker != null) marker.localScale = targetScale;
            RaiseListeningSafe();
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private const float SpinDegreesPerSecond = 6f;

        private void Update()
        {
            if (_globePivot != null)
                _globePivot.Rotate(Vector3.up, SpinDegreesPerSecond * Time.deltaTime, Space.World);
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

            StartCoroutine(RevealNextContinentRoutine());
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[Earth] Session complete: {result}");
        }
    }
}
