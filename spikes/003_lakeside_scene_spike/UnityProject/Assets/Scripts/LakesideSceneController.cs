using System.Collections;
using UnityEngine;

namespace LakesideSceneSpike
{
    /// <summary>
    /// Spike 003: an attempt to get visually closer to the real Kids Hearing Game reference
    /// screenshots (purple karst mountains, huts with lit windows, willows, palms, lily pads,
    /// a pagoda/lantern silhouette, and - most distinctively - a real mirrored water reflection
    /// of the whole scene) shared directly by the project owner. Kept as a separate spike from
    /// 002's three theme-agnostic variants, per instruction, rather than modifying them in place.
    ///
    /// Still 100% procedurally generated in code - no external art, no image-generation tool
    /// available in this environment. The single biggest visual lever pulled here versus spike
    /// 002's dusk variant is a GENUINE mirrored reflection (not just a flat water color): every
    /// scene element built above the waterline gets a flipped, dimmed, tinted duplicate spawned
    /// below it via SpawnReflection(). This is what reads as "lake" rather than "dark rectangle".
    ///
    /// Honest limitation acknowledged up front: no voice narration is attempted here (no
    /// text-to-speech/voice-synthesis tool available in this environment) - only the same
    /// synthesized-tone/chime approach used in spike 002.
    /// </summary>
    public class LakesideSceneController : MonoBehaviour
    {
        private const int TotalRounds = 10;
        private const float MinIdleSeconds = 1.3f;
        private const float MaxIdleSeconds = 2.6f;
        private const float ActiveWindowSeconds = 2.2f;
        private const float ToneFrequencyHz = 900f;
        private const float ToneDurationSeconds = 0.16f;
        private const float WaterLineY = -0.6f;

        private Camera _camera;
        private SpriteRenderer _firefly;
        private Transform _fireflyTransform;
        private ParticleSystem _catchParticles;
        private AudioSource _audioSource;
        private AudioClip _toneClip;
        private AudioClip _chimeClip;

        private int _score;
        private int _round;
        private int _combo;
        private bool _fireflyActive;
        private bool _running = true;
        private float _scorePulseTimer;
        private float _comboTextTimer;
        private Vector3 _comboTextWorldPos;

        private void Awake()
        {
            _camera = BuildCamera();
            BuildSky(_camera);

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            var mountains = BuildMountains(halfWidth, halfHeight * 0.05f);
            var huts = BuildHuts(halfWidth, WaterLineY);
            var willow1 = BuildWillow(new Vector3(-halfWidth * 0.35f, WaterLineY, 0f), 1.1f);
            var willow2 = BuildWillow(new Vector3(halfWidth * 0.55f, WaterLineY, 0f), 0.9f);
            var palm = BuildPalm(new Vector3(-halfWidth * 0.85f, WaterLineY, 0f));
            var lilies = BuildLilyPads(halfWidth, WaterLineY);
            var pagoda = BuildPagoda(new Vector3(-halfWidth * 0.92f, WaterLineY, 0f));

            BuildWater(halfWidth, halfHeight);

            var reflectionTint = new Color(0.72f, 0.78f, 0.95f);
            SpawnReflection(mountains, WaterLineY, 0.4f, reflectionTint, -14);
            SpawnReflection(huts, WaterLineY, 0.45f, reflectionTint, -14);
            SpawnReflection(willow1, WaterLineY, 0.4f, reflectionTint, -14);
            SpawnReflection(willow2, WaterLineY, 0.4f, reflectionTint, -14);
            SpawnReflection(palm, WaterLineY, 0.4f, reflectionTint, -14);
            SpawnReflection(lilies, WaterLineY, 0.5f, reflectionTint, -14);
            SpawnReflection(pagoda, WaterLineY, 0.35f, reflectionTint, -14);

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
            if (_comboTextTimer > 0f) _comboTextTimer -= Time.deltaTime;

            if (!_running) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (_fireflyActive)
                OnCorrectDetection();
        }

        private void OnGUI()
        {
            var white = Color.white;
            var outline = new Color(0.05f, 0.05f, 0.08f);
            var orange = new Color(1f, 0.55f, 0.1f);

            int scoreFontSize = _scorePulseTimer > 0f ? 66 : 46;
            var scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = scoreFontSize, fontStyle = FontStyle.Bold };
            DrawOutlinedLabel(new Rect(30, 30, 500, 100), $"Score: {_score}", scoreStyle, white, outline);

            var roundStyle = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            DrawOutlinedLabel(new Rect(Screen.width - 400, 30, 370, 60), $"{_round}/{TotalRounds}", roundStyle, white, outline);

            if (_comboTextTimer > 0f && _combo >= 2)
            {
                Vector3 risen = _comboTextWorldPos + Vector3.up * (0.7f - _comboTextTimer) * 1.4f;
                Vector3 sp = _camera.WorldToScreenPoint(risen);
                float guiY = Screen.height - sp.y;
                float alpha = Mathf.Clamp01(_comboTextTimer / 0.7f);
                float scale = Mathf.Lerp(1.6f, 1f, alpha);
                var style = new GUIStyle(GUI.skin.label) { fontSize = (int)(52 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                DrawOutlinedLabel(new Rect(sp.x - 150, guiY - 30, 300, 60), $"x{_combo}", style, new Color(orange.r, orange.g, orange.b, alpha), new Color(outline.r, outline.g, outline.b, alpha));
            }

            if (!_running)
            {
                var bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 50, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                DrawOutlinedLabel(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), $"Final score: {_score} ({_round}/{TotalRounds})", bannerStyle, white, outline);
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
                    _combo = 0;
                    yield return StartCoroutine(FadeFireflyOut());
                }
            }

            _running = false;
        }

        private Vector2 RandomFireflyPosition()
        {
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            float x = Random.Range(-halfWidth * 0.6f, halfWidth * 0.6f);
            float y = Random.Range(halfHeight * 0.1f, halfHeight * 0.7f);
            return new Vector2(x, y);
        }

        private void OnCorrectDetection()
        {
            _fireflyActive = false;
            _combo++;
            _score += 100 + (_combo - 1) * 20;
            _scorePulseTimer = 0.3f;
            _comboTextTimer = 0.7f;
            _comboTextWorldPos = _fireflyTransform.position;
            _catchParticles.transform.position = _fireflyTransform.position;
            _catchParticles.Emit(20);
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

        // --- Camera / global setup ---

        private static Camera BuildCamera()
        {
            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.85f, 0.82f, 0.90f);
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
            Color zenith = new Color(0.55f, 0.52f, 0.72f);
            Color mid = new Color(0.80f, 0.74f, 0.85f);
            Color horizon = new Color(0.92f, 0.86f, 0.90f);

            for (int y = 0; y < size; y++)
            {
                float t = y / (float)(size - 1);
                Color c = t < 0.5f ? Color.Lerp(horizon, mid, t / 0.5f) : Color.Lerp(mid, zenith, (t - 0.5f) / 0.5f);
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
            sr.sortingOrder = -20;
            obj.transform.position = Vector3.zero;
            obj.transform.localScale = new Vector3(halfWidth * 2.1f, halfHeight * 2.1f, 1f);

            // A handful of faint stars in the upper sky, matching the reference's pale night sky.
            var starsParent = new GameObject("Stars");
            starsParent.transform.position = Vector3.zero;
            for (int i = 0; i < 18; i++)
            {
                var star = new GameObject("Star");
                star.transform.SetParent(starsParent.transform, false);
                var starSr = star.AddComponent<SpriteRenderer>();
                starSr.sprite = GenerateSolidCircleSprite(16, new Color(1f, 1f, 1f, 0.8f));
                starSr.sortingOrder = -19;
                float x = Random.Range(-halfWidth, halfWidth);
                float y = Random.Range(halfHeight * 0.2f, halfHeight * 0.95f);
                star.transform.position = new Vector3(x, y, 0f);
                float s = Random.Range(0.02f, 0.05f);
                star.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // --- Composite scene elements (each returns its parent GameObject for reflection) ---

        private static GameObject BuildMountains(float halfWidth, float baseY)
        {
            var parent = new GameObject("Mountains");
            parent.transform.position = new Vector3(0f, baseY, 0f);

            Color purpleA = new Color(0.42f, 0.35f, 0.60f);
            Color purpleB = new Color(0.32f, 0.25f, 0.50f);
            Color foliage = new Color(0.28f, 0.48f, 0.32f);

            int peakCount = 6;
            for (int i = 0; i < peakCount; i++)
            {
                float x = Mathf.Lerp(-halfWidth * 1.2f, halfWidth * 1.2f, i / (float)(peakCount - 1)) + Random.Range(-0.35f, 0.35f);
                float height = Random.Range(2.4f, 3.6f);
                float width = Random.Range(1.6f, 2.4f);
                Color c = i % 2 == 0 ? purpleA : purpleB;

                var peakObj = new GameObject("Peak");
                peakObj.transform.SetParent(parent.transform, false);
                var peakSr = peakObj.AddComponent<SpriteRenderer>();
                peakSr.sprite = GenerateTriangleSprite(96, c);
                peakSr.sortingOrder = -16;
                peakObj.transform.localPosition = new Vector3(x, height / 2f, 0f);
                peakObj.transform.localScale = new Vector3(width, height, 1f);

                var capObj = new GameObject("FoliageCap");
                capObj.transform.SetParent(parent.transform, false);
                var capSr = capObj.AddComponent<SpriteRenderer>();
                capSr.sprite = GenerateSolidCircleSprite(96, foliage);
                capSr.sortingOrder = -15;
                capObj.transform.localPosition = new Vector3(x, height * 0.62f, 0f);
                float capR = width * 0.55f;
                capObj.transform.localScale = new Vector3(capR, capR * 0.6f, 1f);
            }
            return parent;
        }

        private static GameObject BuildHuts(float halfWidth, float waterLineY)
        {
            var parent = new GameObject("Huts");
            Color hutBody = new Color(0.15f, 0.10f, 0.10f);
            Color window = new Color(1f, 0.85f, 0.35f);

            int hutCount = 4;
            for (int i = 0; i < hutCount; i++)
            {
                float x = Mathf.Lerp(-halfWidth * 0.75f, halfWidth * 0.9f, i / (float)(hutCount - 1));
                var hutObj = new GameObject("Hut");
                hutObj.transform.SetParent(parent.transform, false);
                hutObj.transform.position = new Vector3(x, waterLineY, 0f);

                var bodyObj = new GameObject("Body");
                bodyObj.transform.SetParent(hutObj.transform, false);
                var bodySr = bodyObj.AddComponent<SpriteRenderer>();
                bodySr.sprite = GenerateSolidRectSprite(hutBody);
                bodySr.sortingOrder = -12;
                bodyObj.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                bodyObj.transform.localScale = new Vector3(0.55f, 0.7f, 1f);

                var roofObj = new GameObject("Roof");
                roofObj.transform.SetParent(hutObj.transform, false);
                var roofSr = roofObj.AddComponent<SpriteRenderer>();
                roofSr.sprite = GenerateTriangleSprite(64, hutBody);
                roofSr.sortingOrder = -12;
                roofObj.transform.localPosition = new Vector3(0f, 0.78f, 0f);
                roofObj.transform.localScale = new Vector3(0.75f, 0.4f, 1f);

                var winObj = new GameObject("Window");
                winObj.transform.SetParent(hutObj.transform, false);
                var winSr = winObj.AddComponent<SpriteRenderer>();
                winSr.sprite = GenerateSolidRectSprite(window);
                winSr.sortingOrder = -11;
                winObj.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                winObj.transform.localScale = new Vector3(0.12f, 0.15f, 1f);
            }
            return parent;
        }

        private static GameObject BuildWillow(Vector3 basePos, float scale)
        {
            var parent = new GameObject("Willow");
            parent.transform.position = basePos;
            Color trunk = new Color(0.30f, 0.20f, 0.15f);
            Color frond = new Color(0.55f, 0.75f, 0.45f);

            var trunkObj = new GameObject("Trunk");
            trunkObj.transform.SetParent(parent.transform, false);
            var trunkSr = trunkObj.AddComponent<SpriteRenderer>();
            trunkSr.sprite = GenerateSolidRectSprite(trunk);
            trunkSr.sortingOrder = -10;
            trunkObj.transform.localPosition = new Vector3(0f, 1.1f * scale, 0f);
            trunkObj.transform.localScale = new Vector3(0.15f * scale, 2.2f * scale, 1f);

            var canopyObj = new GameObject("Canopy");
            canopyObj.transform.SetParent(parent.transform, false);
            var canopySr = canopyObj.AddComponent<SpriteRenderer>();
            canopySr.sprite = GenerateSolidCircleSprite(96, frond);
            canopySr.sortingOrder = -10;
            canopyObj.transform.localPosition = new Vector3(0f, 2.3f * scale, 0f);
            canopyObj.transform.localScale = new Vector3(1.3f * scale, 0.9f * scale, 1f);

            float canopyBottom = 2.3f * scale - 0.45f * scale; // canopy's vertical radius is half its 0.9*scale height
            for (int i = 0; i < 6; i++)
            {
                var frondObj = new GameObject("HangingFrond");
                frondObj.transform.SetParent(parent.transform, false);
                var frondSr = frondObj.AddComponent<SpriteRenderer>();
                frondSr.sprite = GenerateSolidRectSprite(frond);
                frondSr.sortingOrder = -9;
                float ox = Mathf.Lerp(-0.9f, 0.9f, i / 5f) * scale;
                float len = Random.Range(0.5f, 1.1f) * scale;
                // Bottom-pivot rect: placing its bottom edge at (canopyBottom - len) makes the
                // rect's top edge land exactly at canopyBottom, so it visually hangs down from
                // the canopy's underside instead of poking through the middle of the canopy.
                frondObj.transform.localPosition = new Vector3(ox, canopyBottom - len, 0f);
                frondObj.transform.localScale = new Vector3(0.05f * scale, len, 1f);
            }
            return parent;
        }

        private static GameObject BuildPalm(Vector3 basePos)
        {
            var parent = new GameObject("Palm");
            parent.transform.position = basePos;
            Color trunk = new Color(0.35f, 0.25f, 0.15f);
            Color leaf = new Color(0.35f, 0.55f, 0.30f);

            var trunkObj = new GameObject("Trunk");
            trunkObj.transform.SetParent(parent.transform, false);
            var trunkSr = trunkObj.AddComponent<SpriteRenderer>();
            trunkSr.sprite = GenerateSolidRectSprite(trunk);
            trunkSr.sortingOrder = -10;
            trunkObj.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            trunkObj.transform.localScale = new Vector3(0.12f, 2.6f, 1f);

            for (int i = 0; i < 6; i++)
            {
                var leafObj = new GameObject("Leaf");
                leafObj.transform.SetParent(parent.transform, false);
                var leafSr = leafObj.AddComponent<SpriteRenderer>();
                leafSr.sprite = GenerateTriangleSprite(64, leaf);
                leafSr.sortingOrder = -9;
                float angle = Mathf.Lerp(-70f, 70f, i / 5f);
                leafObj.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                leafObj.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                leafObj.transform.localScale = new Vector3(0.5f, 1.1f, 1f);
            }
            return parent;
        }

        private static GameObject BuildLilyPads(float halfWidth, float waterLineY)
        {
            var parent = new GameObject("LilyPads");
            Color pink = new Color(0.95f, 0.55f, 0.70f);
            Color petalLight = new Color(1f, 0.85f, 0.90f);

            int count = 5;
            for (int i = 0; i < count; i++)
            {
                float x = Random.Range(-halfWidth * 0.7f, halfWidth * 0.7f);
                float y = waterLineY - Random.Range(0.4f, 0.8f); // well below the line, so its
                // mirrored reflection (see SpawnReflection) doesn't end up bleeding back above it

                var lilyObj = new GameObject("Lily");
                lilyObj.transform.SetParent(parent.transform, false);
                lilyObj.transform.position = new Vector3(x, y, 0f);

                var padSr = lilyObj.AddComponent<SpriteRenderer>();
                padSr.sprite = GenerateSolidCircleSprite(64, pink);
                padSr.sortingOrder = -8;
                float r = Random.Range(0.12f, 0.2f);
                lilyObj.transform.localScale = new Vector3(r, r * 0.6f, 1f);

                var petalObj = new GameObject("Petal");
                petalObj.transform.SetParent(lilyObj.transform, false);
                var petalSr = petalObj.AddComponent<SpriteRenderer>();
                petalSr.sprite = GenerateSolidCircleSprite(32, petalLight);
                petalSr.sortingOrder = -7;
                petalObj.transform.localPosition = Vector3.zero;
                petalObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            }
            return parent;
        }

        private static GameObject BuildPagoda(Vector3 basePos)
        {
            var parent = new GameObject("Pagoda");
            parent.transform.position = basePos;
            Color silhouette = new Color(0.05f, 0.04f, 0.06f);

            var postObj = new GameObject("Post");
            postObj.transform.SetParent(parent.transform, false);
            var postSr = postObj.AddComponent<SpriteRenderer>();
            postSr.sprite = GenerateSolidRectSprite(silhouette);
            postSr.sortingOrder = -6;
            postObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            postObj.transform.localScale = new Vector3(0.5f, 1f, 1f);

            var roofObj = new GameObject("Roof");
            roofObj.transform.SetParent(parent.transform, false);
            var roofSr = roofObj.AddComponent<SpriteRenderer>();
            roofSr.sprite = GenerateTriangleSprite(64, silhouette);
            roofSr.sortingOrder = -6;
            roofObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            roofObj.transform.localScale = new Vector3(1.1f, 0.6f, 1f);

            var finialObj = new GameObject("Finial");
            finialObj.transform.SetParent(parent.transform, false);
            var finialSr = finialObj.AddComponent<SpriteRenderer>();
            finialSr.sprite = GenerateSolidCircleSprite(32, silhouette);
            finialSr.sortingOrder = -6;
            finialObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            finialObj.transform.localScale = new Vector3(0.15f, 0.25f, 1f);

            return parent;
        }

        private static void BuildWater(float halfWidth, float halfHeight)
        {
            var deepWater = new Color(0.30f, 0.30f, 0.48f);
            var shoreHighlight = new Color(0.85f, 0.85f, 0.92f, 0.6f);

            // GenerateSolidRectSprite uses a bottom-center pivot, so a rect's world footprint runs
            // from `position.y` (bottom edge) upward by `localScale.y` - not centered on position.y.
            float waterBottom = -halfHeight - 0.5f; // a little below the screen edge, just in case
            var waterObj = new GameObject("Water");
            var waterSr = waterObj.AddComponent<SpriteRenderer>();
            waterSr.sprite = GenerateSolidRectSprite(deepWater);
            waterSr.sortingOrder = -18;
            waterObj.transform.position = new Vector3(0f, waterBottom, 0f);
            waterObj.transform.localScale = new Vector3(halfWidth * 2.2f, WaterLineY - waterBottom, 1f);

            var glowObj = new GameObject("Shoreline");
            var glowSr = glowObj.AddComponent<SpriteRenderer>();
            glowSr.sprite = GenerateSolidRectSprite(shoreHighlight);
            glowSr.sortingOrder = -13;
            glowObj.transform.position = new Vector3(0f, WaterLineY - 0.05f, 0f);
            glowObj.transform.localScale = new Vector3(halfWidth * 2.2f, 0.1f, 1f);
        }

        private static void SpawnReflection(GameObject original, float waterLineY, float alphaMultiplier, Color tint, int sortingOrder)
        {
            var reflObj = Object.Instantiate(original);
            reflObj.name = original.name + "_Reflection";
            Vector3 origPos = original.transform.position;
            reflObj.transform.position = new Vector3(origPos.x, 2f * waterLineY - origPos.y, origPos.z);
            Vector3 s = original.transform.localScale;
            reflObj.transform.localScale = new Vector3(s.x, -s.y, s.z);

            foreach (var sr in reflObj.GetComponentsInChildren<SpriteRenderer>())
            {
                var c = sr.color;
                sr.color = new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a * alphaMultiplier);
                sr.sortingOrder = sortingOrder;
            }
        }

        // --- Interactive firefly + particles + audio (same mechanism as spike 002's dusk variant) ---

        private static SpriteRenderer BuildFirefly()
        {
            var obj = new GameObject("MainFirefly");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GenerateGlowSprite(128, new Color(1f, 0.97f, 0.75f));
            sr.material = AdditiveMaterial();
            sr.sortingOrder = 10;
            obj.transform.position = Vector3.zero;
            obj.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
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
            main.startSize = 0.14f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.85f), new Color(1f, 0.9f, 0.5f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = AdditiveMaterial();
            renderer.material.mainTexture = GenerateGlowTexture(64, Color.white);

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
            float[] partials = { 880f, 1320f, 1760f };
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

        private static Sprite GenerateTriangleSprite(int size, Color color)
        {
            var pixels = new Color[size * size];
            Vector2 apex = new Vector2(size / 2f, size - 1f);
            Vector2 baseLeft = new Vector2(1f, 1f);
            Vector2 baseRight = new Vector2(size - 1f, 1f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = PointInTriangle(p, apex, baseLeft, baseRight);
                    pixels[y * size + x] = inside ? color : new Color(0f, 0f, 0f, 0f);
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), size);
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
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
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 4f);
        }
    }
}
