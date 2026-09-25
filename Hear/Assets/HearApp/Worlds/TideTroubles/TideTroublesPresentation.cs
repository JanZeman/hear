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
        // Creatures mid-gag are excluded from AmbientLoop's random bobbing (see AmbientLoop) -
        // both coroutines animating the same transform's position/scale at once looked glitchy.
        private readonly HashSet<Transform> _capturedNow = new();
        private ParticleSystem _captureParticles;
        private AudioSource _audioSource;
        private AudioClip _gagChimeClip;
        private GameObject _net;
        private GameObject _launcherLeft;
        private GameObject _launcherRight;

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
            BuildCaptureGagRig(_camera);

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

        // Comic capture gag, per docs/07-world-tide-troubles.md's
        // "tap accepted -> launch net -> auto-select target -> gag animation", choreographed as
        // five beats (design discussion 2026-09-25): anticipation -> launch -> hit-stop ->
        // exaggerated reaction -> release. ~0.75s total, well inside TrialEngine's 3s
        // listening-safe timeout. All placeholder-shape "greybox" motion (squash/stretch,
        // position lerp, camera shake) - no final art yet, only timing/choreography.
        private IEnumerator CaptureGag(EarChannel channel)
        {
            Transform target = PickTarget(channel);
            if (target == null)
            {
                RaiseListeningSafe();
                yield break;
            }

            _capturedNow.Add(target);
            Vector3 targetPos = target.position;
            Transform launcher = GetLauncher(channel, targetPos);

            // 1. Anticipation: the launcher squashes down before firing - a comic "wind-up" read.
            // No relation to tone timing; this whole sequence only starts after classification.
            yield return Squash(launcher, 0.12f);

            // 2. Launch: net flies from the launcher to the target.
            _net.SetActive(true);
            _net.transform.position = launcher.position;
            yield return FlyTo(_net.transform, launcher.position, targetPos, 0.18f);

            // 3. Hit-stop: a beat of held stillness sells the impact - classic comic timing.
            yield return new WaitForSeconds(0.06f);

            // 4. Exaggerated reaction + the actual reward feedback (particles/chime/shake).
            _captureParticles.transform.position = targetPos;
            _captureParticles.Emit(24);
            _audioSource.PlayOneShot(_gagChimeClip);
            var shake = StartCoroutine(CameraShake(0.15f, 0.12f));
            yield return SquashStretchPop(target, 0.25f);
            yield return shake;

            // 5. Release: net retracts, everything settles back to ambient.
            _net.SetActive(false);
            _capturedNow.Remove(target);
            yield return new WaitForSeconds(0.15f);

            RaiseListeningSafe();
        }

        /// <summary>Left/Right channel launches from the matching side - reinforcing the
        /// ear-aware lateralization <see cref="PickTarget"/> already applies to which creature
        /// gets caught. Combined has no channel bias, so it launches from whichever side is
        /// closer to the auto-selected target instead.</summary>
        private Transform GetLauncher(EarChannel channel, Vector3 targetPos)
        {
            bool useLeft = channel == EarChannel.Left || (channel == EarChannel.Combined && targetPos.x < 0f);
            return (useLeft ? _launcherLeft : _launcherRight).transform;
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
                // Skip creatures currently mid-gag: their transform is already being animated by
                // CaptureGag, and layering the idle bob on top of that looked glitchy.
                var idle = _ambientCreatures.FindAll(c => !_capturedNow.Contains(c));
                if (idle.Count == 0) continue;
                var creature = idle[Random.Range(0, idle.Count)];
                StartCoroutine(BobCreature(creature));
            }
        }

        // --- Capture gag choreography helpers (greybox motion, no final art) ---

        private static IEnumerator Squash(Transform t, float duration)
        {
            Vector3 baseScale = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                float dip = Mathf.Sin(p * Mathf.PI) * 0.3f; // down then back up
                t.localScale = new Vector3(baseScale.x * (1f + dip * 0.5f), baseScale.y * (1f - dip), baseScale.z);
                yield return null;
            }
            t.localScale = baseScale;
        }

        private static IEnumerator FlyTo(Transform t, Vector3 from, Vector3 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = p * p; // ease-in: slow start, fast whip at the end reads as a throw
                t.position = Vector3.Lerp(from, to, eased);
                yield return null;
            }
            t.position = to;
        }

        private static IEnumerator SquashStretchPop(Transform t, float duration)
        {
            Vector3 baseScale = t.localScale;
            Quaternion baseRot = t.rotation;
            // Small per-catch variation (direction/amount) so repeated gags never look identical -
            // this world's whole raison d'etre is comic variety, per docs/07's mood description.
            float spin = Random.Range(-25f, 25f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                // Overshoot-and-settle: pop bigger than base, then bounce back - the "caught!" beat.
                float pop = Mathf.Sin(p * Mathf.PI) * 0.5f * (1f - p * 0.5f);
                t.localScale = baseScale * (1f + pop);
                t.rotation = baseRot * Quaternion.Euler(0f, 0f, spin * Mathf.Sin(p * Mathf.PI));
                yield return null;
            }
            t.localScale = baseScale;
            t.rotation = baseRot;
        }

        private IEnumerator CameraShake(float duration, float magnitude)
        {
            Vector3 basePos = _camera.transform.position;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - elapsed / duration;
                Vector2 offset = Random.insideUnitCircle * magnitude * damper;
                _camera.transform.position = basePos + new Vector3(offset.x, offset.y, 0f);
                yield return null;
            }
            _camera.transform.position = basePos;
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

        /// <summary>Placeholder rig for the capture gag: two dockside "launchers" (left/right, for
        /// ear-aware lateralization) and one reusable "net" that flies between them and whichever
        /// creature gets caught. No real net/launcher/creature art yet - once this choreography is
        /// validated on-device, it needs a designer handoff for real assets (see .agents/roadmap).</summary>
        private void BuildCaptureGagRig(Camera cam)
        {
            float halfHeight = 5f;
            float halfWidth = halfHeight * (cam.aspect > 0 ? cam.aspect : 1.6f);
            float dockY = -halfHeight + 0.9f;

            _launcherLeft = CreateColorQuad("LauncherLeft", new Color(0.45f, 0.32f, 0.2f), 0.5f, 0.5f);
            _launcherLeft.transform.position = new Vector3(-halfWidth * 0.85f, dockY, 0f);

            _launcherRight = CreateColorQuad("LauncherRight", new Color(0.45f, 0.32f, 0.2f), 0.5f, 0.5f);
            _launcherRight.transform.position = new Vector3(halfWidth * 0.85f, dockY, 0f);

            _net = CreateColorQuad("Net", new Color(0.95f, 0.95f, 0.9f), 0.4f, 0.4f);
            _net.SetActive(false);
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
