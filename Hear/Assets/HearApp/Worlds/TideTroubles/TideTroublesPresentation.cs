using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    /// Uses the production concept art from hear-tide-troubles-handoff-v1.0 (sliced at runtime -
    /// see <see cref="TideTroublesArt"/>) instead of the earlier colored-quad greybox. Layering is
    /// controlled entirely via SpriteRenderer.sortingOrder rather than Z/camera transparency-sort
    /// settings, so it is correct regardless of project graphics settings: Background(-100) &lt;
    /// FloatingProps(-10) &lt; Seagulls(-5) &lt; FishTargets(0) &lt; DockFrame(3) &lt;
    /// Net/Launcher(5) &lt; Splash/Effects(9-10) &lt; DogCompanion(15). DockFrame sits between
    /// the fish and the launcher/net/dog/companion/effects on purpose - those all need to read
    /// as standing *on* the dock, in front of its wood texture, not hidden behind it (a real bug
    /// on-device 2026-09-25: DockFrame was frontmost, silently hiding the net/launcher/dog/
    /// companion under its opaque lower portion).
    ///
    /// Ambient gulls/fish/floating props run on their own independent timers (see Update and
    /// FishAmbientLoop) so their motion is never correlated with stimulus onset. A
    /// CorrectDetection triggers an autonomous comic capture regardless of where the player
    /// actually tapped, per the no-aiming-required hard rule. Only fish are valid capture targets
    /// (per the handoff's asset-role split); seagulls and floating props are pure ambience.
    /// </summary>
    public sealed class TideTroublesPresentation : WorldPresentationBase
    {
        private const int FishTargetCount = 6;
        private const int SeagullCount = 3;
        private const int FloatingPropCount = 5;

        private const int IdleDogPoseIndex = 0;
        private const int HappyDogPoseIndex = 5;

        // Handheld.Vibrate() triggers iOS's fixed-length/fixed-intensity system buzz with no way
        // to tune it from C#; a light UIImpactFeedbackGenerator tap (Assets/Plugins/iOS/
        // HearHaptics.mm) reads as noticeably gentler/shorter - human feedback 2026-09-26.
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _HearLightHaptic();
#endif

        private static void LightHaptic()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _HearLightHaptic();
#elif UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
            // No-op on desktop targets (Handheld isn't available on Standalone at all - it's used
            // for local macOS dev-QA builds, see DevAutoQA.cs).
        }

        private sealed class FishInstance
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public int Species;
            public Vector3 RestPosition;
        }

        private sealed class SeagullInstance
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float Speed;
            public float FlapClock;
        }

        private sealed class FloatingPropInstance
        {
            public Transform Transform;
            public Vector3 RestPosition;
            public float Speed;
            public float Amplitude;
            public float Phase;
        }

        private Camera _camera;
        private readonly List<FishInstance> _fishTargets = new();
        private readonly List<SeagullInstance> _seagulls = new();
        private readonly List<FloatingPropInstance> _floatingProps = new();
        // Fish mid-gag are excluded from FishAmbientLoop's random pop (see FishAmbientLoop) -
        // both coroutines animating the same transform's position/sprite at once looked glitchy.
        private readonly HashSet<Transform> _capturedNow = new();
        private ParticleSystem _captureParticles;
        private AudioSource _audioSource;
        private AudioClip _gagChimeClip;
        private GameObject _net;
        private Vector3 _netBaseScale;
        private GameObject _launcherLeft;
        private GameObject _launcherRight;
        private Transform _dog;
        private SpriteRenderer _dogRenderer;
        private Transform _companion;
        private SpriteRenderer _companionRenderer;

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

            BuildBackdrop(_camera);
            SpawnFishTargets(_camera);
            SpawnSeagulls(_camera);
            SpawnFloatingProps(_camera);
            BuildCaptureGagRig(_camera);
            BuildReactionCast(_camera);

            _captureParticles = BuildParticles();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _gagChimeClip = BuildChimeClip();
        }

        private void Start()
        {
            StartCoroutine(FishAmbientLoop());
        }

        private void Update()
        {
            UpdateSeagulls();
            UpdateFloatingProps();
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
        // listening-safe timeout.
        private IEnumerator CaptureGag(EarChannel channel)
        {
            FishInstance fish = PickTarget(channel);
            if (fish == null)
            {
                RaiseListeningSafe();
                yield break;
            }

            _capturedNow.Add(fish.Transform);
            Vector3 targetPos = fish.Transform.position;
            Transform launcher = GetLauncher(channel, targetPos);
            bool launchedFromLeft = launcher == _launcherLeft.transform;

            // 1. Anticipation: the launcher squashes down before firing - a comic "wind-up" read.
            // No relation to tone timing; this whole sequence only starts after classification.
            yield return Squash(launcher, 0.12f);

            // 2. Launch: net flies from the launcher to the target, unfurling from a tight bunch
            // to a fully spread net as it travels (human request 2026-09-26: shoot a tight bundle
            // of net that arrives fully spread) - no new art needed, just scale the existing sprite
            // up over the flight).
            _net.SetActive(true);
            _net.transform.position = launcher.position;
            yield return FlyAndSpreadNet(launcher.position, targetPos, 0.18f);

            // 3. Hit-stop: a beat of held stillness sells the impact - classic comic timing.
            yield return new WaitForSeconds(0.06f);

            // 4. Exaggerated reaction + the actual reward feedback (splash/glow/particles/chime/
            // shake/dog+companion cheer).
            fish.Renderer.sprite = TideTroublesArt.FishCaught(fish.Species);
            SpawnTransientEffect(TideTroublesArt.Splash(Random.Range(0, 3)), targetPos, 0.4f, sortingOrder: 10);
            SpawnTransientEffect(TideTroublesArt.TargetRingGold, targetPos, 0.45f, sortingOrder: 9);
            SpawnTransientEffect(TideTroublesArt.Ripple(Random.Range(0, 2)), targetPos + Vector3.down * 0.15f, 0.5f, sortingOrder: 8);
            _captureParticles.transform.position = targetPos;
            _captureParticles.Emit(24);
            _audioSource.PlayOneShot(_gagChimeClip);
            LightHaptic(); // timed with the camera shake, per device feedback 2026-09-25/26
            var shake = StartCoroutine(CameraShake(0.15f, 0.12f));
            var react = StartCoroutine(ReactionPop(launchedFromLeft));
            yield return SquashStretchPop(fish.Transform, 0.25f);
            yield return shake;
            yield return react;

            // 5. Release: net retracts, everything settles back to ambient.
            _net.SetActive(false);
            _net.transform.localScale = _netBaseScale;
            fish.Renderer.sprite = TideTroublesArt.FishIdle(fish.Species);
            _capturedNow.Remove(fish.Transform);
            yield return new WaitForSeconds(0.15f);

            RaiseListeningSafe();
        }

        /// <summary>Left/Right channel launches from the matching side - reinforcing the
        /// ear-aware lateralization <see cref="PickTarget"/> already applies to which fish gets
        /// caught. Combined has no channel bias, so it launches from whichever side is closer to
        /// the auto-selected target instead.</summary>
        private Transform GetLauncher(EarChannel channel, Vector3 targetPos)
        {
            bool useLeft = channel == EarChannel.Left || (channel == EarChannel.Combined && targetPos.x < 0f);
            return (useLeft ? _launcherLeft : _launcherRight).transform;
        }

        private FishInstance PickTarget(EarChannel channel)
        {
            if (_fishTargets.Count == 0) return null;
            IEnumerable<FishInstance> candidates = _fishTargets;
            if (channel != EarChannel.Combined)
            {
                var side = new List<FishInstance>();
                foreach (var f in _fishTargets)
                {
                    bool onLeft = f.Transform.position.x < 0f;
                    if ((channel == EarChannel.Left && onLeft) || (channel == EarChannel.Right && !onLeft))
                        side.Add(f);
                }
                if (side.Count > 0) candidates = side;
            }
            var list = new List<FishInstance>(candidates);
            return list[Random.Range(0, list.Count)];
        }

        private IEnumerator FishAmbientLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0.6f, 1.8f));
                if (_fishTargets.Count == 0) continue;
                var idle = _fishTargets.FindAll(f => !_capturedNow.Contains(f.Transform));
                if (idle.Count == 0) continue;
                var fish = idle[Random.Range(0, idle.Count)];
                StartCoroutine(FishPop(fish));
            }
        }

        private void UpdateSeagulls()
        {
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            foreach (var g in _seagulls)
            {
                g.Transform.position += Vector3.right * g.Speed * Time.deltaTime;
                g.FlapClock += Time.deltaTime * 6f;
                g.Renderer.sprite = TideTroublesArt.SeagullPose(Mathf.FloorToInt(g.FlapClock));

                float x = g.Transform.position.x;
                if (g.Speed > 0f && x > halfWidth + 1.5f)
                {
                    var p = g.Transform.position;
                    p.x = -halfWidth - 1.5f;
                    g.Transform.position = p;
                }
                else if (g.Speed < 0f && x < -halfWidth - 1.5f)
                {
                    var p = g.Transform.position;
                    p.x = halfWidth + 1.5f;
                    g.Transform.position = p;
                }
            }
        }

        private void UpdateFloatingProps()
        {
            foreach (var prop in _floatingProps)
            {
                float y = prop.RestPosition.y + Mathf.Sin(Time.time * prop.Speed + prop.Phase) * prop.Amplitude;
                prop.Transform.position = new Vector3(prop.RestPosition.x, y, prop.RestPosition.z);
            }
        }

        // --- Capture gag choreography helpers ---

        private IEnumerator FishPop(FishInstance fish)
        {
            Vector3 start = fish.RestPosition;
            float duration = Random.Range(0.4f, 0.8f);
            float height = Random.Range(0.3f, 0.6f);
            fish.Renderer.sprite = TideTroublesArt.FishJump(fish.Species);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                fish.Transform.position = start + Vector3.up * Mathf.Sin(p * Mathf.PI) * height;
                yield return null;
            }
            fish.Transform.position = start;
            if (!_capturedNow.Contains(fish.Transform))
                fish.Renderer.sprite = TideTroublesArt.FishIdle(fish.Species);
        }

        /// <summary>Only the reactor on the side the net was launched from celebrates (companion
        /// sits left, dog sits right) - human feedback 2026-09-25: both popping on every single
        /// catch was more simultaneous animation than the moment needed.</summary>
        private IEnumerator ReactionPop(bool launchedFromLeft)
        {
            Vector3 burstPos = (launchedFromLeft ? _companion : _dog).position + Vector3.up * 0.3f;
            SpawnTransientEffect(TideTroublesArt.CelebrationBurst, burstPos, 0.5f, sortingOrder: 14);
            if (launchedFromLeft)
            {
                _companionRenderer.sprite = TideTroublesArt.CompanionHappy;
                yield return Pop(_companion, 0.3f);
                yield return new WaitForSeconds(0.2f);
                _companionRenderer.sprite = TideTroublesArt.CompanionIdle;
            }
            else
            {
                _dogRenderer.sprite = TideTroublesArt.DogPose(HappyDogPoseIndex);
                yield return Pop(_dog, 0.3f);
                yield return new WaitForSeconds(0.2f);
                _dogRenderer.sprite = TideTroublesArt.DogPose(IdleDogPoseIndex);
            }
        }

        private static IEnumerator Pop(Transform t, float duration)
        {
            Vector3 baseScale = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                float bounce = Mathf.Sin(p * Mathf.PI) * 0.35f * (1f - p * 0.4f);
                t.localScale = baseScale * (1f + bounce);
                yield return null;
            }
            t.localScale = baseScale;
        }

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

        /// <summary>Flies the net from the launcher to the target while scaling it up from a
        /// tight bunch to a fully spread net over the flight - reads as the net unfurling in the
        /// air rather than flying already-open (human request 2026-09-26).</summary>
        private IEnumerator FlyAndSpreadNet(Vector3 from, Vector3 to, float duration)
        {
            Transform t = _net.transform;
            Vector3 bunchedScale = _netBaseScale * 0.45f;
            Vector3 spreadScale = _netBaseScale * 1.7f;
            t.localScale = bunchedScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / duration);
                float eased = p * p; // matches FlyTo's throw-whip easing
                t.position = Vector3.Lerp(from, to, eased);
                t.localScale = Vector3.Lerp(bunchedScale, spreadScale, p); // linear: steady unfurl
                yield return null;
            }
            t.position = to;
            t.localScale = spreadScale;
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

        private void SpawnTransientEffect(Sprite sprite, Vector3 position, float duration, int sortingOrder)
        {
            if (sprite == null) return;
            var obj = new GameObject("Effect");
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            obj.transform.position = position;
            StartCoroutine(FadeAndDestroy(obj, sr, duration));
        }

        private static IEnumerator FadeAndDestroy(GameObject obj, SpriteRenderer sr, float duration)
        {
            Vector3 baseScale = obj.transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                Color c = sr.color;
                c.a = 1f - p;
                sr.color = c;
                obj.transform.localScale = baseScale * (1f + p * 0.4f); // slight expand as it fades, sells impact
                yield return null;
            }
            Destroy(obj);
        }

        // --- Scene construction from real handoff art (see TideTroublesArt) ---

        private static void BuildBackdrop(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float worldW = halfWidth * 2f;
            float worldH = halfHeight * 2f;

            AddCoverSprite("HarborBackground", TideTroublesArt.Background, worldW, worldH, sortingOrder: -100);
            // Dock frame is a foreground overlay (transparent center, wooden posts/rope border) -
            // per the handoff README, it sits in front of the play area.
            AddCoverSprite("DockFrame", TideTroublesArt.DockFrame, worldW, worldH, sortingOrder: 3);
        }

        /// <summary>Scales a sprite uniformly so it fully covers the given world-space rect
        /// (CSS background-size:cover) without distortion - used for the full-bleed
        /// background/dock frame across the Core Safe Square's variable aspect ratio.</summary>
        private static void AddCoverSprite(string name, Sprite sprite, float worldW, float worldH, int sortingOrder)
        {
            var obj = new GameObject(name);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            if (sprite == null) return;
            float spriteW = sprite.rect.width / sprite.pixelsPerUnit;
            float spriteH = sprite.rect.height / sprite.pixelsPerUnit;
            // +3% overscan: a razor-tight cover fit left zero margin on the binding axis, so
            // CameraShake's brief position offset exposed a sliver past the sprite edge (visible
            // as a bright fringe on-device - reported 2026-09-25). The overscan guarantees
            // coverage survives that shake, which is otherwise-unaffected static art.
            float scale = Mathf.Max(worldW / spriteW, worldH / spriteH) * 1.03f;
            obj.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void SpawnFishTargets(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Evenly-spaced, shuffled slots (jittered within each) instead of pure uniform random
            // across the whole width - free random placement clustered fish right next to each
            // other often enough to be worth fixing (human feedback 2026-09-25).
            float rangeMin = -halfWidth * 0.65f;
            float rangeMax = halfWidth * 0.65f;
            float slotWidth = (rangeMax - rangeMin) / FishTargetCount;
            var slotOrder = new List<int>();
            for (int i = 0; i < FishTargetCount; i++) slotOrder.Add(i);
            for (int i = slotOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (slotOrder[i], slotOrder[j]) = (slotOrder[j], slotOrder[i]);
            }

            for (int i = 0; i < FishTargetCount; i++)
            {
                int species = i % TideTroublesArt.FishSpeciesCount;
                var obj = CreateSprite($"Fish_{i}", TideTroublesArt.FishIdle(species), sortingOrder: 0, scale: 2.3f);
                // Background's horizon sits around +0.16*halfHeight in world space (the
                // background/dock cover-fit keeps the full portrait image height on-screen, per
                // docs/v1.0-tide-troubles-handoff/implementation-notes.md); fish must stay below
                // that or they visibly float in the sky/cliffs instead of the water - reported
                // on-device 2026-09-25.
                float slotCenter = rangeMin + slotWidth * (slotOrder[i] + 0.5f);
                float jitterX = Random.Range(-slotWidth * 0.3f, slotWidth * 0.3f);
                Vector3 pos = new(
                    slotCenter + jitterX,
                    Random.Range(-halfHeight * 0.5f, -halfHeight * 0.05f),
                    0f);
                obj.transform.position = pos;
                if (Random.value < 0.5f)
                    obj.transform.localScale = new Vector3(-obj.transform.localScale.x, obj.transform.localScale.y, 1f);

                _fishTargets.Add(new FishInstance
                {
                    Transform = obj.transform,
                    Renderer = obj.GetComponent<SpriteRenderer>(),
                    Species = species,
                    RestPosition = pos,
                });
            }
        }

        private void SpawnSeagulls(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            for (int i = 0; i < SeagullCount; i++)
            {
                var obj = CreateSprite($"Seagull_{i}", TideTroublesArt.SeagullPose(0), sortingOrder: -5, scale: 1.8f);
                float y = Random.Range(halfHeight * 0.45f, halfHeight * 0.85f);
                float x = Random.Range(-halfWidth, halfWidth);
                obj.transform.position = new Vector3(x, y, 0f);

                float speed = Random.Range(1.2f, 2.4f) * (Random.value < 0.5f ? 1f : -1f);
                var sr = obj.GetComponent<SpriteRenderer>();
                sr.flipX = speed < 0f;
                _seagulls.Add(new SeagullInstance
                {
                    Transform = obj.transform,
                    Renderer = sr,
                    Speed = speed,
                    FlapClock = Random.Range(0f, 8f),
                });
            }
        }

        private void SpawnFloatingProps(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            for (int i = 0; i < FloatingPropCount; i++)
            {
                var obj = CreateSprite($"FloatingProp_{i}", TideTroublesArt.FloatingProp(i), sortingOrder: -10, scale: Random.Range(0.7f, 1f));
                bool leftSide = i % 2 == 0;
                float x = leftSide
                    ? Random.Range(-halfWidth * 0.9f, -halfWidth * 0.45f)
                    : Random.Range(halfWidth * 0.45f, halfWidth * 0.9f);
                // Keep below the horizon (~+0.16*halfHeight), same reasoning as SpawnFishTargets.
                float y = Random.Range(-halfHeight * 0.55f, halfHeight * 0.05f);
                Vector3 pos = new(x, y, 0f);
                obj.transform.position = pos;

                _floatingProps.Add(new FloatingPropInstance
                {
                    Transform = obj.transform,
                    RestPosition = pos,
                    Speed = Random.Range(0.8f, 1.6f),
                    Amplitude = Random.Range(0.08f, 0.18f),
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                });
            }
        }

        /// <summary>Placeholder-free rig for the capture gag: two dockside net-cannon launchers
        /// (left/right, for ear-aware lateralization) and one reusable net that flies between
        /// them and whichever fish gets caught. The idle-left/idle-right sheet poses already aim
        /// inward toward center on their respective sides, so no mirroring is needed.</summary>
        private void BuildCaptureGagRig(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float dockY = -halfHeight + 1.1f;

            // Scaled up again to match the enlarged fish/seagull/dog/companion (human feedback
            // 2026-09-25, twice: "strašně malý" then still "pořád moc malé" at 0.85 - the
            // reference concept art shows the net cannon as a large, dominant foreground prop).
            _launcherLeft = CreateSprite("LauncherLeft", TideTroublesArt.LauncherIdleLeft, sortingOrder: 5, scale: 1.4f);
            _launcherLeft.transform.position = new Vector3(-halfWidth * 0.8f, dockY, 0f);

            _launcherRight = CreateSprite("LauncherRight", TideTroublesArt.LauncherIdleRight, sortingOrder: 5, scale: 1.4f);
            _launcherRight.transform.position = new Vector3(halfWidth * 0.8f, dockY, 0f);

            _net = CreateSprite("Net", TideTroublesArt.NetLoose, sortingOrder: 8, scale: 1.1f);
            _netBaseScale = _net.transform.localScale;
            _net.SetActive(false);
        }

        private void BuildReactionCast(Camera cam)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            float dockY = -halfHeight + 1.0f;

            var dogObj = CreateSprite("DogReaction", TideTroublesArt.DogPose(IdleDogPoseIndex), sortingOrder: 15, scale: 1.8f);
            dogObj.transform.position = new Vector3(halfWidth * 0.5f, dockY - 0.15f, 0f);
            _dog = dogObj.transform;
            _dogRenderer = dogObj.GetComponent<SpriteRenderer>();

            // Tucked further under the bottom edge than the dog, not just level with it - human
            // feedback 2026-09-25: level still read as "floating", not "emerging from below" like
            // the dog (the companion sprite's own crop has less visual mass below its pivot, so
            // matching Y alone wasn't enough).
            var companionObj = CreateSprite("CompanionReaction", TideTroublesArt.CompanionIdle, sortingOrder: 15, scale: 2.0f);
            companionObj.transform.position = new Vector3(-halfWidth * 0.5f, dockY - 0.55f, 0f);
            _companion = companionObj.transform;
            _companionRenderer = companionObj.GetComponent<SpriteRenderer>();
        }

        private static GameObject CreateSprite(string name, Sprite sprite, int sortingOrder, float scale = 1f)
        {
            var obj = new GameObject(name);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            obj.transform.localScale = Vector3.one * scale;
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
            renderer.sortingOrder = 12;
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
