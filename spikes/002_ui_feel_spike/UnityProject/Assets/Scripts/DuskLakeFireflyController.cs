using System.Collections;
using UnityEngine;

namespace UiFeelSpike.Dusk
{
    /// <summary>
    /// Spike 002, dusk-lake variant: the most visually ambitious of the three UI feel spike
    /// scenes, evoking (not copying) the "grandpa catches fireflies by a lake" mood referenced
    /// in root README.md section 8c. Everything is still procedurally generated in code - no
    /// external art, no image-generation tool available - this exists specifically to explore
    /// the CEILING of what pure procedural drawing + Unity's built-in particle/blending features
    /// can achieve atmosphere-wise, as a direct comparison against the flat-color calm/energetic
    /// variants.
    ///
    /// Techniques used here that the other two variants don't need:
    /// - A vertical gradient sky (camera background can only be a flat color, so this uses a
    ///   full-screen background sprite instead).
    /// - Silhouette layering (hills, willow trees) for depth.
    /// - A "water" panel with a dimmed mirror of the sky gradient plus a bright shoreline band,
    ///   to suggest reflection without actually rendering a mirrored scene.
    /// - Additive-blended glow sprites (both for the ambient background fireflies and the main
    ///   interactive one) instead of normal alpha blending - this is what makes overlapping
    ///   glows brighten each other instead of just layering flatly, which reads as "magical
    ///   light" rather than "flat sticker".
    /// - A continuously-emitting ambient particle system (decorative only, non-interactive) for
    ///   background fireflies, separate from the single main interactive firefly the player taps.
    /// </summary>
    public class DuskLakeFireflyController : MonoBehaviour
    {
        private const int TotalRounds = 10;
        private const float MinIdleSeconds = 1.3f;
        private const float MaxIdleSeconds = 2.6f;
        private const float ActiveWindowSeconds = 2.2f;
        private const float ToneFrequencyHz = 900f;
        private const float ToneDurationSeconds = 0.16f;

        private Camera _camera;
        private SpriteRenderer _firefly;
        private Transform _fireflyTransform;
        private ParticleSystem _catchParticles;
        private AudioSource _audioSource;
        private AudioClip _toneClip;
        private AudioClip _chimeClip;

        private int _score;
        private int _round;
        private bool _fireflyActive;
        private bool _running = true;
        private float _scorePulseTimer;
        private float _caughtTextTimer;
        private Vector3 _caughtTextWorldPos;

        private void Awake()
        {
            _camera = BuildCamera();
            BuildSky(_camera);
            BuildHills(_camera);
            BuildWillows(_camera);
            BuildLake(_camera);
            BuildAmbientFireflies(_camera);

            _firefly = BuildFirefly();
            _fireflyTransform = _firefly.transform;
            _catchParticles = BuildCatchParticles();

            _toneClip = BuildToneClip(ToneFrequencyHz, ToneDurationSeconds);
            _chimeClip = BuildChimeClip();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            SetFireflyVisible(false);
        }

        private void Start()
        {
            StartCoroutine(GameLoop());
        }

        private void Update()
        {
            if (_scorePulseTimer > 0f) _scorePulseTimer -= Time.deltaTime;
            if (_caughtTextTimer > 0f) _caughtTextTimer -= Time.deltaTime;

            if (!_running) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (_fireflyActive)
                OnCorrectDetection();
        }

        private void OnGUI()
        {
            var warm = new Color(1f, 0.92f, 0.75f);
            var outline = new Color(0.15f, 0.08f, 0.05f);

            int scoreFontSize = _scorePulseTimer > 0f ? 70 : 48;
            var scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = scoreFontSize, fontStyle = FontStyle.Bold };
            DrawOutlinedLabel(new Rect(30, 30, 500, 100), $"Fireflies caught: {_score}", scoreStyle, warm, outline);

            var roundStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            DrawOutlinedLabel(new Rect(Screen.width - 530, 30, 500, 60), $"Round {_round}/{TotalRounds}", roundStyle, warm, outline);

            if (_caughtTextTimer > 0f)
            {
                Vector3 risen = _caughtTextWorldPos + Vector3.up * (0.8f - _caughtTextTimer) * 1.2f;
                Vector3 sp = _camera.WorldToScreenPoint(risen);
                float guiY = Screen.height - sp.y;
                float alpha = Mathf.Clamp01(_caughtTextTimer / 0.8f);
                var style = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                DrawOutlinedLabel(new Rect(sp.x - 150, guiY - 30, 300, 60), "Caught!", style, new Color(1f, 0.95f, 0.6f, alpha), new Color(outline.r, outline.g, outline.b, alpha));
            }

            if (!_running)
            {
                var bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                DrawOutlinedLabel(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), $"A peaceful evening - {_score}/{TotalRounds} fireflies caught", bannerStyle, warm, outline);
            }
        }

        private static void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, Color mainColor, Color outlineColor)
        {
            style.normal.textColor = outlineColor;
            Vector2[] offsets = { new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2), new Vector2(2, 2), new Vector2(0, -2), new Vector2(0, 2), new Vector2(-2, 0), new Vector2(2, 0) };
            foreach (var off in offsets)
                GUI.Label(new Rect(rect.x + off.x, rect.y + off.y, rect.width, rect.height), text, style);
            style.normal.textColor = mainColor;
            GUI.Label(rect, text, style);
        }

        private IEnumerator GameLoop()
        {
            for (_round = 1; _round <= TotalRounds; _round++)
            {
                yield return new WaitForSeconds(Random.Range(MinIdleSeconds, MaxIdleSeconds));

                _fireflyTransform.position = RandomFireflyPosition();
                _fireflyActive = true;
                SetFireflyVisible(true);
                _audioSource.PlayOneShot(_toneClip);
                StartCoroutine(PulseFirefly());

                float elapsed = 0f;
                bool consumed = false;
                while (elapsed < ActiveWindowSeconds)
                {
                    if (!_fireflyActive) { consumed = true; break; }
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!consumed)
                {
                    _fireflyActive = false;
                    yield return StartCoroutine(FadeFireflyOut());
                }
            }

            _running = false;
        }

        private Vector2 RandomFireflyPosition()
        {
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            float x = Random.Range(-halfWidth * 0.7f, halfWidth * 0.7f);
            float y = Random.Range(-halfHeight * 0.1f, halfHeight * 0.65f); // keep above the lake, below the top UI
            return new Vector2(x, y);
        }

        private void OnCorrectDetection()
        {
            _fireflyActive = false;
            _score++;
            _scorePulseTimer = 0.3f;
            _caughtTextTimer = 0.8f;
            _caughtTextWorldPos = _fireflyTransform.position;
            _catchParticles.transform.position = _fireflyTransform.position;
            _catchParticles.Emit(18);
            _audioSource.PlayOneShot(_chimeClip);
            StartCoroutine(CatchFlashAndFade());
        }

        private IEnumerator PulseFirefly()
        {
            float t = 0f;
            while (_fireflyActive)
            {
                t += Time.deltaTime * 3f;
                float s = 1f + Mathf.Sin(t) * 0.18f;
                _fireflyTransform.localScale = Vector3.one * s;
                yield return null;
            }
        }

        private IEnumerator CatchFlashAndFade()
        {
            const float flashDuration = 0.15f;
            float t = 0f;
            while (t < flashDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 2.2f, t / flashDuration);
                _fireflyTransform.localScale = Vector3.one * s;
                yield return null;
            }
            yield return StartCoroutine(FadeFireflyOut());
        }

        private IEnumerator FadeFireflyOut()
        {
            const float duration = 0.35f;
            float t = 0f;
            float startAlpha = _firefly.color.a;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(startAlpha, 0f, t / duration);
                var c = _firefly.color;
                _firefly.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            SetFireflyVisible(false);
        }

        private void SetFireflyVisible(bool visible)
        {
            var c = _firefly.color;
            c.a = visible ? 1f : 0f;
            _firefly.color = c;
            _fireflyTransform.localScale = Vector3.one;
        }

        // --- Runtime scene/asset construction ---

        private static Camera BuildCamera()
        {
            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.05f, 0.15f);
            camObj.AddComponent<AudioListener>();
            return cam;
        }

        private static Material AdditiveMaterial()
        {
            Shader shader = Shader.Find("Particles/Additive")
                            ?? Shader.Find("Legacy Shaders/Particles/Additive")
                            ?? Shader.Find("Sprites/Default");
            return new Material(shader);
        }

        private static void BuildSky(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            const int size = 256;
            var pixels = new Color[size * size];
            Color zenith = new Color(0.10f, 0.06f, 0.22f);
            Color midSky = new Color(0.28f, 0.18f, 0.38f);
            Color duskOrange = new Color(0.85f, 0.42f, 0.32f);
            Color horizonPink = new Color(0.96f, 0.68f, 0.52f);

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1); // 0 = bottom (horizon), 1 = top (zenith)
                Color c;
                if (t < 0.12f) c = Color.Lerp(horizonPink, duskOrange, t / 0.12f);
                else if (t < 0.45f) c = Color.Lerp(duskOrange, midSky, (t - 0.12f) / 0.33f);
                else c = Color.Lerp(midSky, zenith, (t - 0.45f) / 0.55f);

                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = c;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

            var obj = new GameObject("Sky");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -10;
            obj.transform.position = Vector3.zero;
            obj.transform.localScale = new Vector3(halfWidth * 2.1f, halfHeight * 2.1f, 1f);
        }

        private static void BuildHills(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Color farHill = new Color(0.14f, 0.10f, 0.22f, 0.9f);
            Color nearHill = new Color(0.06f, 0.045f, 0.10f);

            SpawnHillLayer(halfWidth, -halfHeight * 0.05f, farHill, -6, 1.1f);
            SpawnHillLayer(halfWidth, -halfHeight * 0.18f, nearHill, -5, 0.9f);
        }

        private static void SpawnHillLayer(float halfWidth, float baseY, Color color, int sortingOrder, float heightScale)
        {
            var parent = new GameObject("HillLayer");
            parent.transform.position = new Vector3(0f, baseY, 0f);
            int bumpCount = 5;
            for (int i = 0; i < bumpCount; i++)
            {
                float x = Mathf.Lerp(-halfWidth * 1.3f, halfWidth * 1.3f, i / (float)(bumpCount - 1));
                x += Random.Range(-0.4f, 0.4f);
                float radius = Random.Range(2.2f, 3.4f);
                var sprite = GenerateSolidCircleSprite(96, color);
                var obj = new GameObject("Hill");
                obj.transform.SetParent(parent.transform, false);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = sortingOrder;
                obj.transform.localPosition = new Vector3(x, 0f, 0f);
                obj.transform.localScale = new Vector3(radius, radius * heightScale, 1f);
            }
        }

        private static void BuildWillows(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Color silhouette = new Color(0.04f, 0.03f, 0.06f);

            SpawnWillow(new Vector3(-halfWidth * 0.82f, -halfHeight * 0.05f, 0f), 1.0f, silhouette, -4);
            SpawnWillow(new Vector3(halfWidth * 0.86f, -halfHeight * 0.02f, 0f), 1.2f, silhouette, -4);
        }

        private static void SpawnWillow(Vector3 basePos, float scale, Color color, int sortingOrder)
        {
            var trunkSprite = GenerateSolidRectSprite(color);
            var trunkObj = new GameObject("WillowTrunk");
            trunkObj.transform.position = basePos;
            var trunkSr = trunkObj.AddComponent<SpriteRenderer>();
            trunkSr.sprite = trunkSprite;
            trunkSr.sortingOrder = sortingOrder;
            trunkObj.transform.localScale = new Vector3(0.18f * scale, 2.2f * scale, 1f);
            trunkObj.transform.position += new Vector3(0f, 1.1f * scale, 0f);

            var canopySprite = GenerateSolidCircleSprite(96, color);
            for (int i = 0; i < 4; i++)
            {
                var canopyObj = new GameObject("WillowCanopy");
                var sr = canopyObj.AddComponent<SpriteRenderer>();
                sr.sprite = canopySprite;
                sr.sortingOrder = sortingOrder;
                float ox = Random.Range(-0.5f, 0.5f) * scale;
                float oy = Random.Range(1.6f, 2.4f) * scale;
                canopyObj.transform.position = basePos + new Vector3(ox, oy, 0f);
                float r = Random.Range(0.9f, 1.3f) * scale;
                canopyObj.transform.localScale = new Vector3(r, r * 0.8f, 1f);
            }
        }

        private static void BuildLake(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            const float lakeHeight = 2.6f;

            var deepWater = new Color(0.05f, 0.06f, 0.12f);
            var shoreGlow = new Color(0.35f, 0.28f, 0.30f, 0.7f);

            var lakeObj = new GameObject("Lake");
            var lakeSr = lakeObj.AddComponent<SpriteRenderer>();
            lakeSr.sprite = GenerateSolidRectSprite(deepWater);
            lakeSr.sortingOrder = -3;
            lakeObj.transform.position = new Vector3(0f, -halfHeight + lakeHeight / 2f, 0f);
            lakeObj.transform.localScale = new Vector3(halfWidth * 2.2f, lakeHeight, 1f);

            var glowObj = new GameObject("Shoreline");
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GenerateSolidRectSprite(shoreGlow);
            glowSr.sortingOrder = -2;
            glowObj.transform.position = new Vector3(0f, -halfHeight + lakeHeight, 0f);
            glowObj.transform.localScale = new Vector3(halfWidth * 2.2f, 0.15f, 1f);
        }

        private static void BuildAmbientFireflies(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            var obj = new GameObject("AmbientFireflies");
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.8f, 0.3f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 3f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(halfWidth * 1.6f, halfHeight * 1.1f, 0.1f);
            obj.transform.position = new Vector3(0f, -halfHeight * 0.05f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.4f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = AdditiveMaterial();
            renderer.material.mainTexture = GenerateGlowTexture(64, new Color(1f, 0.95f, 0.6f));

            ps.Play();
        }

        private static SpriteRenderer BuildFirefly()
        {
            var obj = new GameObject("MainFirefly");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GenerateGlowSprite(128, new Color(1f, 0.97f, 0.75f));
            sr.material = AdditiveMaterial();
            sr.sortingOrder = 5;
            obj.transform.position = Vector3.zero;
            obj.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            return sr;
        }

        private static ParticleSystem BuildCatchParticles()
        {
            var obj = new GameObject("CatchParticles");
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 1.5f;
            main.startSize = 0.12f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.4f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = AdditiveMaterial();
            renderer.material.mainTexture = GenerateGlowTexture(64, new Color(1f, 0.95f, 0.6f));

            ps.Play();
            return ps;
        }

        private static AudioClip BuildToneClip(float frequencyHz, float durationSeconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            float fadeSamples = sampleRate * 0.015f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, Mathf.Min(i / fadeSamples, (sampleCount - i) / fadeSamples));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * 0.4f * envelope;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildChimeClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.5f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("Chime", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            float[] partials = { 880f, 1320f, 1760f }; // simple major-ish chime chord
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 4f);
                float sample = 0f;
                foreach (float f in partials)
                    sample += Mathf.Sin(2f * Mathf.PI * f * t);
                data[i] = (sample / partials.Length) * 0.5f * envelope;
            }
            clip.SetData(data, 0);
            return clip;
        }

        // --- Procedural sprite/texture generation ---

        private static Sprite GenerateSolidCircleSprite(int size, Color color)
        {
            var pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01((radius - dist) / 2f) * color.a;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite GenerateGlowSprite(int size, Color coreColor)
        {
            var pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                    // bright core, quadratic falloff to fully transparent at the edge - reads well with additive blending
                    float falloff = Mathf.Clamp01(1f - dist);
                    float alpha = falloff * falloff;
                    pixels[y * size + x] = new Color(coreColor.r, coreColor.g, coreColor.b, alpha);
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Texture2D GenerateGlowTexture(int size, Color coreColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
                    float falloff = Mathf.Clamp01(1f - dist);
                    float alpha = falloff * falloff;
                    tex.SetPixel(x, y, new Color(coreColor.r, coreColor.g, coreColor.b, alpha));
                }
            tex.Apply();
            return tex;
        }

        private static Sprite GenerateSolidRectSprite(Color color)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }
    }
}
