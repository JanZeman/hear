using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.VikingBoat
{
    /// <summary>
    /// World 5 - Viking Boat. Another scratch space in the same spirit as
    /// <see cref="HearApp.Worlds.Experiment.ExperimentPresentation"/> (human request 2026-09-26:
    /// "Priprav dalsi svet, nazvi jej Viking Boat. Objekty dodam za chvili") - camera/lighting are
    /// ready, BuildEnvironment is an empty placeholder until a model/texture pack arrives.
    ///
    /// When adding the real asset, follow the pattern proven out on the Experiment world's Castle
    /// pass: copy the FBX into Resources/Worlds/VikingBoat/Models, copy any texture pack into
    /// Resources/Worlds/VikingBoat/Textures, measure the imported hierarchy once via a throwaway
    /// Editor diagnostic (mesh/camera/light counts, combined bounds, material names) rather than
    /// guessing, strip any embedded camera/light the FBX brings with it (a recurring gotcha - an
    /// embedded camera silently composited over ours with Unity's default sky both times so far),
    /// and wire textures onto fresh URP Lit materials by matching the FBX's material names to the
    /// texture pack's file names.
    /// </summary>
    public sealed class VikingBoatPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private float _startTime;

        private void Awake()
        {
            _startTime = Time.time;
            BuildLighting();
            BuildEnvironment();
            BuildCamera();
            BuildShieldRewardAssets();
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.38f);
            RenderSettings.ambientIntensity = 1f;

            var lightObject = new GameObject("VikingBoatSun");
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
            var cameraObject = new GameObject("VikingBoatCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.55f, 0.68f, 0.78f);
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 300f;
            _camera.fieldOfView = 42f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
            // Position/orientation are driven every frame by Update() (flythrough) instead of
            // set once here.
        }

        // Flythrough: the camera glides along the hull's side at shield height so each shield on
        // the gunwale crosses the frame one at a time as we pass it (human request 2026-09-27:
        // "Dokazes tu lod ... nechat proplout tak, aby se ty stity mihaly jeden po druhem?").
        // The ship is recentred in BuildEnvironment so its combined bounds sit at local origin;
        // measured combined size was (1.65, 2.39, 4.58), so half-length along Z is ~2.3m - the
        // span below adds a margin past both ends so shields visibly enter/exit frame.
        // Kept within the flat midship section (not the curled bow/stern, which sit outside this
        // span) so the rail stays level and the shields pass by at a consistent height/spacing.
        private const float FlythroughHalfSpan = 1.7f;
        private const float FlythroughSideOffset = 2.6f;
        private const float FlythroughHeight = -0.5f;
        private const float FlythroughDuration = 8f;

        private void Update()
        {
            if (_camera == null) return;

            float elapsed = Time.time - _startTime;
            float normalized = Mathf.PingPong(elapsed, FlythroughDuration) / FlythroughDuration;
            float z = Mathf.Lerp(-FlythroughHalfSpan, FlythroughHalfSpan, normalized);

            _camera.transform.position = new Vector3(FlythroughSideOffset, FlythroughHeight, z);
            _camera.transform.LookAt(new Vector3(-0.4f, FlythroughHeight, z), Vector3.up);
        }

        // Reward shields. Original idea (human, 2026-09-27): "s kazdym uspechem se bud objevi
        // nebo 'prileti' jeden shield na bok lodi. Hezky velky aby bylo videt jakou ma texturu."
        // Refined immediately after seeing the first pass (human, same session): "Ty stity mohou
        // byt videt od zacatku - a hooodne blizko oka uzivatele. A pri 'uspechu' by se mohli
        // 'pripnout' na lod." - so instead of spawning + cutting the camera away to it, the next
        // shield to be earned is always held close in view (parented to the camera, so it rides
        // along through the ambient flythrough) and on a correct detection it flies from there
        // over to its slot on the hull and snaps into place; the following variant then appears
        // held, ready for the next success.
        //
        // The shield FBX's raw mesh data (measured via a throwaway InspectShieldModel Editor
        // diagnostic using MeshFilter.sharedMesh.bounds, i.e. true local/object space - NOT
        // Renderer.bounds, which is a world AABB and was misleading here) is a disc lying in the
        // local XY plane, thin along local Z: mesh.bounds size (2.15, 2.17, 0.26). The imported
        // asset's default transform.localRotation (270, 0, 0) is a separate FBX-import correction
        // baked onto the root - assigning our own localRotation below REPLACES that default
        // rather than composing with it, so the rotation here is derived straight from the raw
        // mesh axes, not from the default-oriented appearance. Euler(0, 90, 0) sends local Z (the
        // disc's thin/normal axis) to world X - a shield mounted on the hull's side, facing
        // outward - while local Y (wide) becomes world Y (vertical) and local X (wide) becomes
        // world Z (along the ship's length).
        private const float ShieldNativeDiameter = 2.15f;
        private const float ShieldTargetDiameter = 0.6f;
        private const float ShieldScale = ShieldTargetDiameter / ShieldNativeDiameter;
        private static readonly Quaternion ShieldMountRotation = Quaternion.Euler(0f, 90f, 0f);
        private const int ShieldVariantCount = 6;

        // Evenly spaced along the same flat midship span used by the flythrough, at the same
        // rail height/offset already validated by the QA screenshots.
        private static readonly Vector3[] ShieldSlotPositions =
        {
            new(0.9f, -0.5f, -1.7f),
            new(0.9f, -0.5f, -1.02f),
            new(0.9f, -0.5f, -0.34f),
            new(0.9f, -0.5f, 0.34f),
            new(0.9f, -0.5f, 1.02f),
            new(0.9f, -0.5f, 1.7f),
        };

        // Held close in front of the camera (child of the camera transform, so it rides along
        // through the flythrough) - "hooodne blizko oka uzivatele". Facing the camera means the
        // mesh's local-Z normal (see rotation note above) must point along local -Z here, with
        // local Y kept as up: Euler(0, 180, 0) does exactly that.
        private static readonly Vector3 HeldShieldLocalPosition = new(0f, -0.1f, 1.0f);
        private static readonly Quaternion HeldShieldLocalRotation = Quaternion.Euler(0f, 180f, 0f);

        private GameObject _shieldAsset;
        private Material[] _shieldMaterials;
        private Transform _heldShield;
        private int _nextVariantIndex;
        private int _shieldsPlaced;

        private void BuildShieldRewardAssets()
        {
            _shieldAsset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShield");
            if (_shieldAsset == null)
            {
                Debug.LogError("[VikingBoat] Could not load VikingShield model from Resources.");
                return;
            }

            _shieldMaterials = new Material[ShieldVariantCount];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            for (int i = 0; i < ShieldVariantCount; i++)
            {
                var material = new Material(shader);
                material.mainTexture = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/Shields/Shield{i + 1}_Albedo");
                var normalMap = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/Shields/Shield{i + 1}_Normal");
                if (normalMap != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_BumpMap", normalMap);
                }

                _shieldMaterials[i] = material;
            }

            SpawnHeldShield();
        }

        private void SpawnHeldShield()
        {
            if (_shieldAsset == null || _nextVariantIndex >= _shieldMaterials.Length) return;

            var instance = Instantiate(_shieldAsset, _camera.transform);
            instance.name = $"HeldShield_{_nextVariantIndex}";
            instance.transform.localPosition = HeldShieldLocalPosition;
            instance.transform.localRotation = HeldShieldLocalRotation;
            instance.transform.localScale = Vector3.one * ShieldScale;

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _shieldMaterials[_nextVariantIndex];

            _heldShield = instance.transform;
            _nextVariantIndex++;
        }

        private IEnumerator AttachShieldRoutine()
        {
            if (_heldShield == null || _shieldsPlaced >= ShieldSlotPositions.Length)
            {
                RaiseListeningSafe();
                yield break;
            }

            Transform shield = _heldShield;
            _heldShield = null;
            Vector3 targetPosition = ShieldSlotPositions[_shieldsPlaced++];

            // Detach from the camera, keeping its current world pose as the flight's start point.
            shield.SetParent(transform, true);
            Vector3 startPosition = shield.localPosition;
            Quaternion startRotation = shield.localRotation;

            const float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                shield.localPosition = Vector3.Lerp(startPosition, targetPosition, eased);
                shield.localRotation = Quaternion.Slerp(startRotation, ShieldMountRotation, eased);
                yield return null;
            }

            shield.localPosition = targetPosition;
            shield.localRotation = ShieldMountRotation;

            SpawnHeldShield();
            RaiseListeningSafe();
        }

        private static readonly Vector3 SourceBoundsCenter = new(0f, 1.17f, -0.2f);

        private void BuildEnvironment()
        {
            var asset = Resources.Load<GameObject>("Worlds/VikingBoat/Models/VikingShip");
            if (asset == null)
            {
                Debug.LogError("[VikingBoat] Could not load VikingShip model from Resources.");
                return;
            }

            var pivot = new GameObject("VikingShip").transform;
            pivot.SetParent(transform, false);

            var instance = Instantiate(asset);
            instance.transform.SetParent(pivot, false);
            instance.transform.localPosition = -SourceBoundsCenter;

            // No embedded camera/light this time (the plain .obj format has no concept of either,
            // unlike the Experiment world's FBX assets) - stripped defensively anyway in case a
            // future asset for this world does ship as FBX.
            foreach (var importedCamera in instance.GetComponentsInChildren<Camera>(true))
                Destroy(importedCamera.gameObject);
            foreach (var importedLight in instance.GetComponentsInChildren<Light>(true))
                Destroy(importedLight.gameObject);

            ApplyTextures(instance);
        }

        // The three materials (map_ShipV_001/002/003) each map onto their own BaseColor + Normal
        // texture pair, copied from the source ship_textures.zip into
        // Assets/HearApp/Resources/Worlds/VikingBoat/Textures/ with simplified names.
        private static readonly (string materialName, string textureBaseName)[] ShipMaterials =
        {
            ("map_ShipV_001", "Ship001"),
            ("map_ShipV_002", "Ship002"),
            ("map_ShipV_003", "Ship003"),
        };

        private readonly Dictionary<string, Material> _materialCache = new();

        private void ApplyTextures(GameObject instance)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var original = renderer.sharedMaterials;
                var replaced = new Material[original.Length];
                for (int i = 0; i < original.Length; i++)
                    replaced[i] = original[i] != null ? BuildMaterialFor(original[i].name) : null;
                renderer.sharedMaterials = replaced;
            }
        }

        private Material BuildMaterialFor(string materialName)
        {
            if (_materialCache.TryGetValue(materialName, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);

            var entry = System.Array.Find(ShipMaterials, e => e.materialName == materialName);
            if (entry.textureBaseName != null)
            {
                material.mainTexture = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/{entry.textureBaseName}_BaseColor");
                var normalMap = Resources.Load<Texture2D>($"Worlds/VikingBoat/Textures/{entry.textureBaseName}_Normal");
                if (normalMap != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_BumpMap", normalMap);
                }
            }
            else
            {
                material.color = Color.gray;
            }

            _materialCache[materialName] = material;
            return material;
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

            StartCoroutine(AttachShieldRoutine());
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[VikingBoat] Session complete: {result}");
        }
    }
}
