using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;

namespace HearApp.Worlds.MycMurmur
{
    /// <summary>
    /// World 9 - Mycelium Murmur. Dev scratch world (human request 2026-09-27, after the free
    /// "Hand Painted Tiles - Sleeping Forest" Unity pack turned up ground textures, richer
    /// glow-capable mushroom models, grass, and a firefly effect: "Zaloz toto jako uplne novy
    /// svet... snive poeticky nazev" between mushrooms and sound) - a dusk forest clearing, its
    /// own thing next to the plain low-poly "Mushrooms" world rather than a replacement for it.
    ///
    /// The pack's mushroom models have no usable diffuse texture (the only non-AO/Normal file,
    /// "Mushrooms_Diffuse.tif", is actually a leaf/grass detail atlas that doesn't correspond to
    /// the mushroom UVs at all - confirmed by inspecting both at full resolution) - so colour
    /// comes from tinting a shared AO texture per instance instead, which needs no exact UV
    /// match and still reads as proper hand-painted shading.
    /// </summary>
    public sealed class MycMurmurPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private Texture2D _mushroomAo;
        private Texture2D _mushroomNormal;
        private Texture2D[] _mushroomEmission;
        private readonly Dictionary<string, Material> _mushroomMaterialCache = new();

        private void Awake()
        {
            BuildLighting();
            BuildGround();
            LoadMushroomTextures();
            BuildStaticGrass();
            BuildMushroomSlots();
            BuildFireflies();
            BuildCamera();
        }

        // Shared between the camera's clear colour and fog, so the ground plane's edge (still
        // physically there - it's a finite primitive Plane) fades into an exact colour match
        // instead of being visible as a hard horizon line (human report 2026-09-27: "u ZADNE ze
        // scen bych nechtel, aby byl videt ten zakladovy ctverec").
        private static readonly Color SkyColor = new(0.05f, 0.06f, 0.12f);

        private void BuildLighting()
        {
            // Dusk/night mood, matching the pack's own "Sleeping Forest" night variant and
            // setting up the glowing mushrooms/fireflies to actually read against something dark.
            // Brightened a notch from the first pass (human report: "Podhoubi neni zase tak moc
            // podhoubi") so the ground texture actually reads instead of sitting near-black.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.17f, 0.24f);
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = SkyColor;
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 15f;

            var moonObject = new GameObject("MycMurmurMoon");
            moonObject.transform.SetParent(transform, false);
            moonObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.68f, 0.75f, 0.95f);
            moon.intensity = 0.75f;
            moon.shadows = LightShadows.Soft;
        }

        private const float PatchRadius = 6f;

        // Vastly larger than the mushroom patch or the camera's orbit (see GrowthRadius/
        // OrbitRadius below) so its edge sits well past where fog has already faded it to
        // SkyColor - the plane never visibly ends, regardless of camera angle.
        private const float GroundRadius = 40f;
        private const float GroundTextureTiling = 22f;

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = Vector3.one * (GroundRadius * 2f / 10f);
            Destroy(ground.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = new Color(0.8f, 0.85f, 0.7f) };
            var groundTex = Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/GroundGrass");
            material.mainTexture = groundTex;
            material.mainTextureScale = new Vector2(GroundTextureTiling, GroundTextureTiling);
            var groundNormal = Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/GroundGrassNormal");
            if (groundNormal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", groundNormal);
                material.SetTextureScale("_BumpMap", new Vector2(GroundTextureTiling, GroundTextureTiling));
                material.SetFloat("_BumpScale", 1.6f);
            }
            ground.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void LoadMushroomTextures()
        {
            _mushroomAo = Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/MushroomAO");
            _mushroomNormal = Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/MushroomNormal");
            _mushroomEmission = new[]
            {
                Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/MushroomEmissionA"),
                Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/MushroomEmissionB"),
                Resources.Load<Texture2D>("Worlds/MycMurmur/Textures/MushroomEmissionC"),
            };
        }

        private static readonly string[] GrassSpecies = { "GrassA", "GrassB", "GrassC" };
        private const int GrassCount = 26;

        private void BuildStaticGrass()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var grassMaterial = new Material(shader) { color = new Color(0.32f, 0.42f, 0.22f) };

            for (int i = 0; i < GrassCount; i++)
            {
                string species = GrassSpecies[i % GrassSpecies.Length];
                var asset = Resources.Load<GameObject>($"Worlds/MycMurmur/Models/{species}");
                if (asset == null) continue;

                var instance = Instantiate(asset, transform);
                instance.name = $"{species}_{i}";
                Vector2 offset = Random.insideUnitCircle * PatchRadius;
                instance.transform.localPosition += new Vector3(offset.x, 0f, offset.y);
                instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
                instance.transform.localScale *= Random.Range(0.8f, 1.4f);

                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = grassMaterial;
            }
        }

        // A hand-picked toadstool palette (the pack's own diffuse texture isn't usable - see the
        // class doc) rather than one flat colour, so the patch still reads as varied at a glance.
        private static readonly Color[] CapPalette =
        {
            new(0.71f, 0.20f, 0.16f), // red
            new(0.85f, 0.52f, 0.16f), // orange
            new(0.55f, 0.35f, 0.62f), // purple
            new(0.55f, 0.40f, 0.24f), // tan/brown
            new(0.69f, 0.27f, 0.44f), // deep pink
            new(0.74f, 0.60f, 0.16f), // pale gold
        };

        private static readonly string[] MushroomSpecies = { "Mushroom1", "Mushroom2", "Mushroom3", "MushroomsA", "MushroomsB" };
        private const int MushroomCount = 20;

        // Same camera-centered placement fix proven in the Mushrooms world (human report
        // 2026-09-27: growth must happen "pred ocima uzivatele... ne mimo vysec") - kept within a
        // radius small enough, relative to the orbit's distance from its fixed look target, to
        // stay well inside the FOV cone regardless of the camera's current orbit angle.
        private const float GrowthRadius = 2.5f;

        private readonly List<Transform> _mushroomSlots = new();
        private readonly List<Vector3> _mushroomTargetScales = new();
        private int _revealIndex;

        private void BuildMushroomSlots()
        {
            for (int i = 0; i < MushroomCount; i++)
            {
                string species = MushroomSpecies[i % MushroomSpecies.Length];
                var asset = Resources.Load<GameObject>($"Worlds/MycMurmur/Models/{species}");
                if (asset == null)
                {
                    Debug.LogError($"[MycMurmur] Could not load '{species}' from Resources.");
                    continue;
                }

                var instance = Instantiate(asset, transform);
                instance.name = $"{species}_{i}";

                Vector2 offset = Random.insideUnitCircle * GrowthRadius;
                instance.transform.localPosition += new Vector3(offset.x, 0f, offset.y);
                instance.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
                instance.transform.localScale *= Random.Range(0.85f, 1.3f);

                Color tint = CapPalette[i % CapPalette.Length];
                bool glow = i % 3 == 0;
                var material = BuildMushroomMaterial(tint, glow, glow ? _mushroomEmission[(i / 3) % _mushroomEmission.Length] : null);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = material;

                Vector3 targetScale = instance.transform.localScale;
                instance.transform.localScale = Vector3.zero;
                _mushroomSlots.Add(instance.transform);
                _mushroomTargetScales.Add(targetScale);
            }
        }

        private Material BuildMushroomMaterial(Color tint, bool glow, Texture2D emissionTexture)
        {
            string cacheKey = $"{tint}|{glow}|{(emissionTexture != null ? emissionTexture.name : "none")}";
            if (_mushroomMaterialCache.TryGetValue(cacheKey, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { color = tint };
            material.mainTexture = _mushroomAo;
            if (_mushroomNormal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", _mushroomNormal);
            }
            if (glow && emissionTexture != null)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", emissionTexture);
                material.SetColor("_EmissionColor", tint * 1.6f);
            }

            _mushroomMaterialCache[cacheKey] = material;
            return material;
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

        // Ambient, always-on (not reward-gated) - the pack's own Fireflies.prefab couldn't be
        // reused directly (this project builds everything procedurally at runtime, no prefab
        // pipeline - see every other world), so this is a small hand-rolled equivalent: warm
        // gold points drifting slowly within the growth patch.
        private void BuildFireflies()
        {
            var fireflyObject = new GameObject("Fireflies");
            fireflyObject.transform.SetParent(transform, false);
            fireflyObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var particles = fireflyObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.16f);
            main.startColor = new Color(0.95f, 0.85f, 0.35f, 0.85f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.rateOverTime = 3f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(GrowthRadius * 2.2f, 1.2f, GrowthRadius * 2.2f);

            var velocityOverLifetime = particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            // All three axes must share one curve mode (Unity logs "Particle Velocity curves
            // must all be in the same mode" every frame otherwise, found 2026-09-27) - x/z get a
            // small wander range too instead of being left at their differently-moded default.
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.05f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            // Without a texture, a particle billboard is a flat hard-edged quad - "svetlusky
            // nejsou svetlusky ale zlute ctverce" (human report 2026-09-27). A soft radial-alpha
            // dot generated at runtime (no asset needed, consistent with this world's procedural
            // build) turns it into a proper glowing point instead - but the URP Lit-style manual
            // "_SURFACE_TYPE_TRANSPARENT" keyword recipe used elsewhere this session for
            // transparency (Earth's cloud/atmo shells, Saturn's ring) never actually rendered
            // transparent for any of them either, so this uses Sprites/Default instead: a plain,
            // always-alpha-blended shader with no keyword configuration needed at all.
            var shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = new Color(1f, 0.9f, 0.5f) };
            material.mainTexture = CreateSoftDotTexture(32);

            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        private static Texture2D CreateSoftDotTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(size - 1, size - 1) * 0.5f;
            // Distance to the square's own INSCRIBED circle radius (size/2), not to its corner -
            // using the corner distance left the square's corners themselves only mostly-faded
            // rather than fully zero, invisible on a small/distant sprite but reading as a hard
            // square edge once a close-up particle magnified it (human report 2026-09-27, after
            // the first pass: distant fireflies looked round, near ones still looked square).
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / radius);
                    float alpha = Mathf.SmoothStep(0f, 1f, t);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();
            return texture;
        }

        private void BuildCamera()
        {
            var cameraObject = new GameObject("MycMurmurCamera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = SkyColor;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 100f;
            _camera.fieldOfView = 58f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CoreSafeSquareFit>();
        }

        // Same gentle orbit as the Mushrooms world's proven "diorama turntable" camera, pulled
        // in closer/lower (human report 2026-09-27: "Muzeme se do toho sveta mnohem vice
        // ponorit?") for a more immersive, inside-the-clearing feel rather than a distant
        // overview.
        private const float OrbitRadius = 4.8f;
        private const float OrbitHeight = 2.2f;
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
            Debug.Log($"[MycMurmur] Session complete: {result}");
        }
    }
}
