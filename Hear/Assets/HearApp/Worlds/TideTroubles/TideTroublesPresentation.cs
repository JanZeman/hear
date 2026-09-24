using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;

namespace HearApp.Worlds.TideTroubles
{
    /// <summary>
    /// World 1 - Tide Troubles (2D). This presentation has zero knowledge of tone
    /// timing, scheduling, or classification.
    /// It only reacts to <see cref="PresentOutcome"/> calls the engine makes after the fact.
    ///
    /// Ambient gulls/fish/balloon run on their own independent random timers (see AmbientLoop) so
    /// their motion is never correlated with stimulus onset. A CorrectDetection triggers an
    /// autonomous comic capture regardless of where the player actually tapped, per the
    /// no-aiming-required hard rule.
    /// </summary>
    public sealed class TideTroublesPresentation : WorldPresentationBase
    {
        private Camera _camera;
        private readonly List<Transform> _ambientCreatures = new();
        private ParticleSystem _captureParticles;
        private AudioSource _audioSource;
        private AudioClip _gagChimeClip;

        private float _sessionProgress;

        private void Awake()
        {
            var camObj = new GameObject("TideTroublesCamera") { tag = "MainCamera" };
            camObj.transform.position = new Vector3(0f, 0f, -10f);
            _camera = camObj.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.53f, 0.81f, 0.94f);
            camObj.AddComponent<AudioListener>();
            camObj.AddComponent<CoreSafeSquareFit>();

            BuildHarborDock(_camera);
            SpawnAmbientCreatures(_camera);

            _captureParticles = BuildParticles();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _gagChimeClip = BuildChimeClip();
        }

        private void Start()
        {
            StartCoroutine(AmbientLoop());
        }

        public override void Initialize(WorldContext context)
        {
            _sessionProgress = 0f;
        }

        public override void PresentOutcome(OutcomePresentationContext outcome)
        {
            if (outcome.Outcome == TrialOutcome.CorrectDetection)
                StartCoroutine(CaptureGag(outcome.Channel));
            else
                RaiseListeningSafe(); // Miss/FalsePositive/CorrectRejection: stay quiet, no punishment.
        }

        public override void SetSessionProgress(float normalizedProgress)
        {
            _sessionProgress = normalizedProgress;
            // Harbor could evolve (time of day, background boats) with progress independent of
            // correctness; kept as a placeholder hook for this architecture slice.
        }

        public override void CompleteSession(SessionResult result)
        {
            Debug.Log($"[TideTroubles] Session complete: {result}");
        }

        private IEnumerator CaptureGag(EarChannel channel)
        {
            // Autonomous capture: pick a target preferentially on the classified side (post-
            // classification lateralization only), otherwise any ambient creature - the player's
            // actual tap location never matters.
            Transform target = PickTarget(channel);
            Vector3 pos = target != null ? target.position : Vector3.zero;

            _captureParticles.transform.position = pos;
            _captureParticles.Emit(24);
            _audioSource.PlayOneShot(_gagChimeClip);

            yield return new WaitForSeconds(0.35f); // disruptive phase duration
            RaiseListeningSafe();
        }

        private Transform PickTarget(EarChannel channel)
        {
            if (_ambientCreatures.Count == 0) return null;
            IEnumerable<Transform> candidates = _ambientCreatures;
            if (channel != EarChannel.Combined)
            {
                var side = new List<Transform>();
                foreach (var c in _ambientCreatures)
                {
                    bool onLeft = c.position.x < 0f;
                    if ((channel == EarChannel.Left && onLeft) || (channel == EarChannel.Right && !onLeft))
                        side.Add(c);
                }
                if (side.Count > 0) candidates = side;
            }
            var list = new List<Transform>(candidates);
            return list[Random.Range(0, list.Count)];
        }

        private IEnumerator AmbientLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.6f, 1.8f));
                if (_ambientCreatures.Count == 0) continue;
                var creature = _ambientCreatures[Random.Range(0, _ambientCreatures.Count)];
                StartCoroutine(BobCreature(creature));
            }
        }

        private static IEnumerator BobCreature(Transform t)
        {
            Vector3 start = t.position;
            float duration = Random.Range(0.4f, 0.8f);
            float height = Random.Range(0.3f, 0.7f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                t.position = start + Vector3.up * Mathf.Sin(p * Mathf.PI) * height;
                yield return null;
            }
            t.position = start;
        }

        // --- Placeholder procedural scene construction ---

        private static void BuildHarborDock(Camera cam)
        {
            float halfHeight = cam.orthographicSize > 0 ? cam.orthographicSize : 5f;
            float halfWidth = halfHeight * (cam.aspect > 0 ? cam.aspect : 1.6f);
            var dock = CreateColorQuad("Dock", new Color(0.55f, 0.4f, 0.28f), halfWidth * 2.2f, 1.4f);
            dock.transform.position = new Vector3(0f, -halfHeight + 0.7f, 0f);
        }

        private void SpawnAmbientCreatures(Camera cam)
        {
            float halfHeight = 5f;
            float halfWidth = halfHeight * (cam.aspect > 0 ? cam.aspect : 1.6f);
            Color[] palette =
            {
                new(0.95f, 0.85f, 0.2f), // gull
                new(0.3f, 0.55f, 0.85f), // fish
                new(0.9f, 0.3f, 0.25f)   // crab/balloon
            };
            for (int i = 0; i < 6; i++)
            {
                var obj = CreateColorQuad($"AmbientCreature_{i}", palette[i % palette.Length], 0.6f, 0.6f);
                obj.transform.position = new Vector3(
                    Random.Range(-halfWidth * 0.7f, halfWidth * 0.7f),
                    Random.Range(-halfHeight * 0.2f, halfHeight * 0.6f),
                    0f);
                _ambientCreatures.Add(obj.transform);
            }
        }

        private static GameObject CreateColorQuad(string name, Color color, float width, float height)
        {
            var tex = new Texture2D(2, 2);
            var pixels = new[] { color, color, color, color };
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);

            var obj = new GameObject(name);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            obj.transform.localScale = new Vector3(width, height, 1f);
            return obj;
        }

        private static ParticleSystem BuildParticles()
        {
            var obj = new GameObject("CaptureParticles");
            var ps = obj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.6f;
            main.startSpeed = 4f;
            main.startSize = 0.15f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.8f, 0.2f));
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                throw new System.InvalidOperationException("The URP particle shader is unavailable.");
            renderer.material = new Material(shader);
            ps.Play();
            return ps;
        }

        private static AudioClip BuildChimeClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.2f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var clip = AudioClip.Create("TideChime", sampleCount, 1, sampleRate, false);
            var data = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float progress = t / duration;
                float freq = Mathf.Lerp(700f, 1200f, progress);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.5f * (1f - progress);
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}
