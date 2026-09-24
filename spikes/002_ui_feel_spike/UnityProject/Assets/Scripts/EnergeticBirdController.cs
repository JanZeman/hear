using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UiFeelSpike.Energetic
{
    /// <summary>
    /// Spike 002, energetic variant: same throwaway-exploration purpose as GameFeelController
    /// (get a hands-on feel for Unity before committing to final theme art), but pushed toward
    /// the "Angry Birds" / "Moorhuhn" energetic register requested for comparison against the
    /// calm variant - a procedurally-drawn angry bird pops up at a random screen position each
    /// round instead of a plain circle in the center, with punchier hit feedback.
    ///
    /// Important design note: the bird's random position is purely a VISUAL/engagement layer.
    /// The actual detection input is still "tap ANYWHERE while active counts as correct" (same
    /// rule as the calm variant), matching the audiologically-validated go/no-go detection
    /// mechanic from root README.md section 4 - this does NOT turn into a precision-aiming/
    /// motor-skill task, which would no longer be measuring hearing. Hit feedback (particles,
    /// popup text) is drawn at the bird's actual position regardless of where exactly the tap
    /// landed, to sell the "you popped the bird" fantasy without requiring aim accuracy.
    /// </summary>
    public class EnergeticBirdController : MonoBehaviour
    {
        private const int TotalRounds = 10;
        private const float MinIdleSeconds = 1.0f;
        private const float MaxIdleSeconds = 2.2f;
        private const float ActiveWindowSeconds = 1.8f;
        private const float ToneFrequencyHz = 600f;
        private const float ToneDurationSeconds = 0.12f;

        private class Popup
        {
            public Vector3 WorldPos;
            public float Timer;
            public string Text;
        }

        private SpriteRenderer _stimulus;
        private Transform _stimulusTransform;
        private ParticleSystem _hitParticles;
        private AudioSource _audioSource;
        private AudioClip _toneClip;
        private AudioClip _chirpClip;
        private Camera _camera;
        private readonly List<Popup> _popups = new List<Popup>();

        private int _score;
        private int _round;
        private bool _stimulusActive;
        private bool _running = true;
        private float _scorePulseTimer;
        private float _flashAlpha;

        private void Awake()
        {
            _camera = BuildCamera();
            BuildGround(_camera);
            _stimulus = BuildStimulus();
            _stimulusTransform = _stimulus.transform;
            _hitParticles = BuildParticles();
            _toneClip = BuildToneClip(ToneFrequencyHz, ToneDurationSeconds);
            _chirpClip = BuildChirpClip();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            SetStimulusVisible(false);
        }

        private void Start()
        {
            StartCoroutine(GameLoop());
        }

        private void Update()
        {
            if (_scorePulseTimer > 0f)
                _scorePulseTimer -= Time.deltaTime;
            if (_flashAlpha > 0f)
                _flashAlpha = Mathf.Max(0f, _flashAlpha - Time.deltaTime * 2.5f);

            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                _popups[i].Timer -= Time.deltaTime;
                if (_popups[i].Timer <= 0f)
                    _popups.RemoveAt(i);
            }

            if (!_running) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (_stimulusActive)
                OnCorrectDetection();
            else
                OnFalsePositive();
        }

        private void OnGUI()
        {
            if (_flashAlpha > 0.001f)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.15f, 0.1f, _flashAlpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prevColor;
            }

            var outline = new Color(0.25f, 0.08f, 0.02f);
            var main = new Color(1f, 0.98f, 0.9f);

            int scoreFontSize = _scorePulseTimer > 0f ? 76 : 52;
            var scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = scoreFontSize, fontStyle = FontStyle.Bold };
            DrawOutlinedLabel(new Rect(30, 30, 500, 100), $"Score: {_score}", scoreStyle, main, outline);

            var roundStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            DrawOutlinedLabel(new Rect(Screen.width - 530, 30, 500, 60), $"Round {_round}/{TotalRounds}", roundStyle, main, outline);

            var popupStyle = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            foreach (var p in _popups)
            {
                Vector3 risenWorld = p.WorldPos + Vector3.up * (0.8f - p.Timer) * 1.5f;
                Vector3 screenPoint = _camera.WorldToScreenPoint(risenWorld);
                float guiY = Screen.height - screenPoint.y;
                float alpha = Mathf.Clamp01(p.Timer / 0.8f);
                var col = new Color(1f, 0.85f, 0.1f, alpha);
                DrawOutlinedLabel(new Rect(screenPoint.x - 150, guiY - 30, 300, 60), p.Text, popupStyle, col, new Color(outline.r, outline.g, outline.b, alpha));
            }

            if (!_running)
            {
                var bannerStyle = new GUIStyle(GUI.skin.label) { fontSize = 60, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                DrawOutlinedLabel(new Rect(0, Screen.height / 2f - 60, Screen.width, 120), $"Done! Final score: {_score}/{TotalRounds}", bannerStyle, main, outline);
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

                _stimulusTransform.position = RandomSpawnPosition();
                _stimulusActive = true;
                SetStimulusVisible(true);
                _audioSource.PlayOneShot(_toneClip);
                StartCoroutine(WobbleStimulus());

                float elapsed = 0f;
                bool consumed = false;
                while (elapsed < ActiveWindowSeconds)
                {
                    if (!_stimulusActive) { consumed = true; break; }
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (!consumed)
                {
                    _stimulusActive = false;
                    yield return StartCoroutine(MissFade());
                }
            }

            _running = false;
        }

        private Vector2 RandomSpawnPosition()
        {
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            const float sideMargin = 0.75f;   // keep away from screen edges
            const float topExclude = 0.78f;   // keep clear of score/round text
            const float bottomExclude = 0.6f; // keep clear of the ground strip
            float x = Random.Range(-halfWidth * sideMargin, halfWidth * sideMargin);
            float y = Random.Range(-halfHeight * bottomExclude, halfHeight * topExclude);
            return new Vector2(x, y);
        }

        private void OnCorrectDetection()
        {
            _stimulusActive = false;
            _score++;
            _scorePulseTimer = 0.3f;
            _popups.Add(new Popup { WorldPos = _stimulusTransform.position, Timer = 0.8f, Text = "POP!" });
            _hitParticles.transform.position = _stimulusTransform.position;
            _hitParticles.Emit(30);
            _audioSource.PlayOneShot(_chirpClip);
            StartCoroutine(PopAndFade());
            StartCoroutine(CameraShake(0.16f, 0.12f));
        }

        private void OnFalsePositive()
        {
            _flashAlpha = 0.3f;
        }

        private IEnumerator WobbleStimulus()
        {
            float t = 0f;
            while (_stimulusActive)
            {
                t += Time.deltaTime * 9f;
                float angle = Mathf.Sin(t) * 12f;
                float scale = 1f + Mathf.Sin(t * 1.3f) * 0.08f;
                _stimulusTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                _stimulusTransform.localScale = Vector3.one * scale;
                yield return null;
            }
        }

        private IEnumerator PopAndFade()
        {
            const float squashDuration = 0.08f;
            float t = 0f;
            while (t < squashDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(1f, 0.55f, t / squashDuration);
                _stimulusTransform.localScale = new Vector3(s * 1.4f, s * 0.65f, 1f);
                yield return null;
            }

            const float popDuration = 0.18f;
            t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float p = t / popDuration;
                float s = Mathf.Lerp(0.65f, 1.9f, p);
                float a = Mathf.Lerp(1f, 0f, p);
                _stimulusTransform.localScale = Vector3.one * s;
                var c = _stimulus.color;
                _stimulus.color = new Color(c.r, c.g, c.b, a);
                yield return null;
            }
            SetStimulusVisible(false);
        }

        private IEnumerator MissFade()
        {
            const float duration = 0.25f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(1f, 0f, t / duration);
                float s = Mathf.Lerp(1f, 0.7f, t / duration);
                var c = _stimulus.color;
                _stimulus.color = new Color(c.r, c.g, c.b, a);
                _stimulusTransform.localScale = Vector3.one * s;
                yield return null;
            }
            SetStimulusVisible(false);
        }

        private void SetStimulusVisible(bool visible)
        {
            var c = _stimulus.color;
            c.a = visible ? 1f : 0f;
            _stimulus.color = c;
            _stimulusTransform.localScale = Vector3.one;
            _stimulusTransform.localRotation = Quaternion.identity;
        }

        private IEnumerator CameraShake(float duration, float magnitude)
        {
            Vector3 originalPos = _camera.transform.localPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float falloff = 1f - t / duration;
                float x = Random.Range(-1f, 1f) * magnitude * falloff;
                float y = Random.Range(-1f, 1f) * magnitude * falloff;
                _camera.transform.localPosition = originalPos + new Vector3(x, y, 0f);
                yield return null;
            }
            _camera.transform.localPosition = originalPos;
        }

        // --- Runtime scene/asset construction ---

        private static Camera BuildCamera()
        {
            var camObj = new GameObject("Main Camera") { tag = "MainCamera" };
            camObj.transform.position = new Vector3(0f, 0f, -10f); // sprites sit at z=0; without this the
            // camera defaults to (0,0,0) and every sprite ends up ON/behind its near clip plane, so nothing
            // ever actually renders (only the camera's own background color and OnGUI overlay are visible).
            var cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.53f, 0.81f, 0.94f); // bright cartoon sky blue
            camObj.AddComponent<AudioListener>();
            return cam;
        }

        private static void BuildGround(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            const float groundHeight = 1.6f;

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            var grassGreen = new Color(0.35f, 0.72f, 0.28f);
            for (int i = 0; i < 16; i++) pixels[i] = grassGreen;
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);

            var obj = new GameObject("Ground");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -1;
            obj.transform.position = new Vector3(0f, -halfHeight + groundHeight / 2f, 0f);
            obj.transform.localScale = new Vector3(halfWidth * 2.2f, groundHeight, 1f);
        }

        private static SpriteRenderer BuildStimulus()
        {
            var obj = new GameObject("AngryBird");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = GenerateBirdSprite(200);
            obj.transform.position = Vector3.zero;
            return sr;
        }

        private static ParticleSystem BuildParticles()
        {
            var obj = new GameObject("HitParticles");
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.7f;
            main.startSpeed = 4.5f;
            main.startSize = 0.18f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.2f), new Color(0.85f, 0.15f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));

            ps.Play();
            return ps;
        }

        private static AudioClip BuildToneClip(float frequencyHz, float durationSeconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
            var clip = AudioClip.Create("Tone", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            float fadeSamples = sampleRate * 0.01f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Min(1f, Mathf.Min(i / fadeSamples, (sampleCount - i) / fadeSamples));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequencyHz * t) * 0.5f * envelope;
            }
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip BuildChirpClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.18f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("Chirp", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(900f, 150f, progress);
                float envelope = 1f - progress;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f * envelope;
            }
            clip.SetData(data, 0);
            return clip;
        }

        // --- Procedural bird sprite drawing ---

        private static Sprite GenerateBirdSprite(int size)
        {
            var pixels = new Color[size * size];
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float r = size * 0.36f;

            Color red = new Color(0.82f, 0.14f, 0.09f);
            Color darkRed = new Color(0.5f, 0.07f, 0.05f);
            Color white = Color.white;
            Color black = new Color(0.08f, 0.08f, 0.08f);
            Color orange = new Color(1f, 0.55f, 0.05f);

            FillCircle(pixels, size, center, r, red);
            FillEllipse(pixels, size, center + new Vector2(0f, -r * 0.22f), r * 0.55f, r * 0.42f, white);

            FillTriangle(pixels, size,
                center + new Vector2(-r * 0.05f, r * 0.78f),
                center + new Vector2(r * 0.22f, r * 0.8f),
                center + new Vector2(r * 0.05f, r * 1.18f),
                darkRed);

            FillRotatedRect(pixels, size, center + new Vector2(-r * 0.32f, r * 0.3f), r * 0.55f, r * 0.16f, -22f, black);
            FillRotatedRect(pixels, size, center + new Vector2(r * 0.32f, r * 0.3f), r * 0.55f, r * 0.16f, 22f, black);

            Vector2 eyeOffset = new Vector2(r * 0.28f, r * 0.06f);
            FillCircle(pixels, size, center - eyeOffset, r * 0.19f, white);
            FillCircle(pixels, size, center + new Vector2(eyeOffset.x, eyeOffset.y), r * 0.19f, white);
            FillCircle(pixels, size, center - eyeOffset, r * 0.09f, black);
            FillCircle(pixels, size, center + new Vector2(eyeOffset.x, eyeOffset.y), r * 0.09f, black);

            FillTriangle(pixels, size,
                center + new Vector2(-r * 0.18f, -r * 0.02f),
                center + new Vector2(r * 0.18f, -r * 0.02f),
                center + new Vector2(0f, -r * 0.36f),
                orange);

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void BlendPixel(Color[] pixels, int texSize, int x, int y, Color color, float coverage)
        {
            if (x < 0 || x >= texSize || y < 0 || y >= texSize || coverage <= 0f) return;
            int idx = y * texSize + x;
            Color dst = pixels[idx];
            float outA = coverage + dst.a * (1f - coverage);
            if (outA <= 0.0001f) { pixels[idx] = new Color(0f, 0f, 0f, 0f); return; }
            float outR = (color.r * coverage + dst.r * dst.a * (1f - coverage)) / outA;
            float outG = (color.g * coverage + dst.g * dst.a * (1f - coverage)) / outA;
            float outB = (color.b * coverage + dst.b * dst.a * (1f - coverage)) / outA;
            pixels[idx] = new Color(outR, outG, outB, outA);
        }

        private static void FillCircle(Color[] pixels, int texSize, Vector2 center, float radius, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1));
            int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.x + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1));
            int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.y + radius + 1));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float coverage = Mathf.Clamp01(radius - dist + 1f);
                    BlendPixel(pixels, texSize, x, y, color, coverage);
                }
        }

        private static void FillEllipse(Color[] pixels, int texSize, Vector2 center, float rx, float ry, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - rx - 1));
            int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.x + rx + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - ry - 1));
            int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.y + ry + 1));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float nx = (x + 0.5f - center.x) / rx;
                    float ny = (y + 0.5f - center.y) / ry;
                    float d = nx * nx + ny * ny;
                    if (d <= 1f) BlendPixel(pixels, texSize, x, y, color, 1f);
                }
        }

        private static void FillRotatedRect(Color[] pixels, int texSize, Vector2 center, float width, float height, float angleDeg, Color color)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(-rad), sin = Mathf.Sin(-rad);
            float halfDiag = Mathf.Sqrt(width * width + height * height) * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(center.x - halfDiag - 1));
            int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.x + halfDiag + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(center.y - halfDiag - 1));
            int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(center.y + halfDiag + 1));
            float hw = width * 0.5f, hh = height * 0.5f;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - center.x;
                    float dy = y + 0.5f - center.y;
                    float lx = dx * cos - dy * sin;
                    float ly = dx * sin + dy * cos;
                    if (Mathf.Abs(lx) <= hw && Mathf.Abs(ly) <= hh)
                        BlendPixel(pixels, texSize, x, y, color, 1f);
                }
        }

        private static void FillTriangle(Color[] pixels, int texSize, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(texSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (PointInTriangle(p, a, b, c))
                        BlendPixel(pixels, texSize, x, y, color, 1f);
                }
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
    }
}
