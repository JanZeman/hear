using System;
using System.Collections;
using System.Collections.Generic;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HearApp.Core.HearingEngine
{
    /// <summary>
    /// Owns stimulus timing, audio routing, response capture, trial classification, and stored
    /// measurement results. This is the single source of truth for the hearing measurement -
    /// worlds are downstream observers only, via <see cref="IWorldPresentation"/>.
    ///
    /// <see cref="ProcessTrial"/> is the shared core: both the real scheduling loop
    /// (<see cref="RunSession"/>) and the development mock driver call through this exact method,
    /// so "the same deterministic sequence produces the same stored SessionResult in every world"
    /// is true by construction rather than by convention.
    /// </summary>
    public sealed class TrialEngine : MonoBehaviour
    {
        private const float MinIdleSeconds = 1.0f;
        private const float MaxIdleSeconds = 2.2f;
        private const float ActiveWindowSeconds = 2.0f;
        private const float ListeningSafeTimeoutSeconds = 3.0f;

        public SessionResult CurrentResult { get; private set; } = new SessionResult();
        public bool IsRunning { get; private set; }
        public float Progress { get; private set; }

        /// <summary>Points the most recent CorrectDetection earned - see <see
        /// cref="PointsForFrequency"/>. Simple placeholder scoring (human request 2026-09-26:
        /// "udělej to teď jen jednoduše" - do it simply for now); real scoring needs actual
        /// per-trial volume/audibility data, not just the reference frequency. Tracked here as
        /// roadmap item 009.</summary>
        public int LastAwardedPoints { get; private set; }

        /// <summary>Optional hook the Shell wires up so a tap that lands on interactive HUD
        /// chrome (e.g. the pause button) isn't also counted as a hearing-test response - human
        /// report 2026-09-26: tapping pause scored points. Takes the raw screen position of the
        /// press; returns true to swallow it. The engine has no UI Toolkit dependency of its own,
        /// so this stays a plain delegate rather than a direct reference to Shell/UI types.</summary>
        public Func<Vector2, bool> IsScreenPointOverBlockingUI;

        /// <summary>True once a session has been ended early by <see
        /// cref="AbortSessionFranticTapping"/> - its results should be shown as uncounted, not as
        /// a normal completed session.</summary>
        public bool SessionInvalidatedByFranticTapping { get; private set; }

        /// <summary>True once the player has deliberately ended a session early via the pause
        /// menu's "Quit to Home" (human request 2026-09-26: "dovol hru přerušit a navrátit se") -
        /// distinct from <see cref="SessionInvalidatedByFranticTapping"/> so the Shell can show a
        /// neutral message instead of a warning.</summary>
        public bool SessionEndedByUserQuit { get; private set; }

        /// <summary>Fires with the strike number (1, 2, 3) each time a burst of rapid/frantic
        /// tapping is detected within <see cref="FranticTapWindowSeconds"/> - human request
        /// 2026-09-26: pause + warn on strikes 1-2, end the session (uncounted) on strike 3. The
        /// Shell owns all the pause/warning/results UI this drives; the engine only detects and
        /// (on strike 3) aborts.</summary>
        public event Action<int> FranticTappingStrike;

        // More sensitive than the original 8-taps/2.5s (human feedback 2026-09-26: it only
        // triggered after a truly excessive number of taps).
        private const int FranticTapCountThreshold = 5;
        private const float FranticTapWindowSeconds = 1.8f;

        private IWorldPresentation _world;
        private TonePlayer _tonePlayer;
        private bool _tapReceived;
        private bool _abortRequested;
        private int _franticStrikeCount;
        private readonly List<float> _recentTapTimes = new();
        private float _sessionElapsedSeconds;
        private float _estimatedTotalSeconds;

        private void Awake()
        {
            _tonePlayer = gameObject.AddComponent<TonePlayer>();
        }

        private void Update()
        {
            if (!IsRunning) return;

            if (Pointer.current?.press.wasPressedThisFrame == true)
            {
                // Tracked regardless of pause state or what's under the finger - frantic tapping
                // through a warning pause, or aimed at the pause button itself, is still frantic
                // tapping. Unscaled time so the window keeps working correctly across a
                // Time.timeScale == 0 pause.
                TrackFranticTapping(Time.unscaledTime);

                // Time.timeScale == 0 is the Playing HUD's pause button (ShellUIController); a
                // tap landing while paused must not bleed into the trial that resumes.
                if (Time.timeScale > 0f)
                {
                    Vector2 pos = Pointer.current.position.ReadValue();
                    bool blockedByUi = IsScreenPointOverBlockingUI != null && IsScreenPointOverBlockingUI(pos);
                    if (!blockedByUi)
                        _tapReceived = true;
                }
            }

            // Time-based, continuously advancing rather than jumping once per completed trial -
            // the discrete per-trial jump read as an unintended "tap now" cue on-device (human
            // feedback 2026-09-25: "jako kdyby dával impuls: klikni na obrazovku"). Capped below
            // 1 while running so it never *looks* finished before CompleteSession actually snaps
            // it to 1.
            _sessionElapsedSeconds += Time.deltaTime;
            Progress = Mathf.Min(0.97f, _sessionElapsedSeconds / Mathf.Max(0.01f, _estimatedTotalSeconds));
        }

        /// <summary>Attaches a world and resets session state. Call once per world entry.</summary>
        /// <param name="targetSessionSeconds">Every world's session now runs for exactly this
        /// long (human request 2026-09-26: "at v jakemkoli svete tvrva presne 30 sekund"),
        /// cycling through the trial plan as many times as it takes rather than stopping once a
        /// fixed trial count is exhausted - see <see cref="RunSession"/>. The progress bar tracks
        /// real elapsed time against this exact value, not an estimate.</param>
        public void BeginSession(IWorldPresentation world, WorldContext context, float targetSessionSeconds)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            CurrentResult = new SessionResult();
            _estimatedTotalSeconds = Mathf.Max(1f, targetSessionSeconds);
            _sessionElapsedSeconds = 0f;
            Progress = 0f;
            _abortRequested = false;
            SessionInvalidatedByFranticTapping = false;
            SessionEndedByUserQuit = false;
            _franticStrikeCount = 0;
            _recentTapTimes.Clear();
            IsRunning = true;
            _world.Initialize(context);
        }

        private void TrackFranticTapping(float nowUnscaled)
        {
            _recentTapTimes.Add(nowUnscaled);
            _recentTapTimes.RemoveAll(t => nowUnscaled - t > FranticTapWindowSeconds);
            if (_recentTapTimes.Count < FranticTapCountThreshold) return;

            _recentTapTimes.Clear(); // a fresh burst is needed to trigger the next strike
            _franticStrikeCount++;
            FranticTappingStrike?.Invoke(_franticStrikeCount);
            if (_franticStrikeCount >= 3)
                AbortSessionFranticTapping();
        }

        /// <summary>Ends the session early and marks it uncounted - called automatically on the
        /// third frantic-tapping strike (see <see cref="FranticTappingStrike"/>).</summary>
        public void AbortSessionFranticTapping()
        {
            if (!IsRunning) return;
            _abortRequested = true;
            SessionInvalidatedByFranticTapping = true;
            IsRunning = false;
            _world.SetSessionProgress(1f);
            _world.CompleteSession(CurrentResult);
        }

        /// <summary>Ends the session early because the player chose to, via the pause menu - see
        /// <see cref="SessionEndedByUserQuit"/>.</summary>
        public void AbortSessionUserQuit()
        {
            if (!IsRunning) return;
            _abortRequested = true;
            SessionEndedByUserQuit = true;
            IsRunning = false;
            _world.SetSessionProgress(1f);
            _world.CompleteSession(CurrentResult);
        }

        /// <summary>Runs the real audio-driven trial loop, cycling through the plan (reshuffled
        /// each lap) for exactly <see cref="_estimatedTotalSeconds"/> real seconds - the plan
        /// itself is just one small pool of trial specs to draw from, not a fixed session length
        /// anymore (human request 2026-09-26). The final trial in progress is allowed to finish
        /// normally rather than being cut off mid-tone/mid-gag, so a session may run a little past
        /// the target by however long one trial takes.</summary>
        public IEnumerator RunSession(List<TrialSpec> plan)
        {
            int i = 0;
            while (_sessionElapsedSeconds < _estimatedTotalSeconds)
            {
                if (_abortRequested) yield break; // AbortSessionFranticTapping/UserQuit already completed things

                if (i >= plan.Count)
                {
                    Shuffle(plan);
                    i = 0;
                }
                var spec = plan[i++];

                yield return new WaitForSeconds(UnityEngine.Random.Range(MinIdleSeconds, MaxIdleSeconds));
                if (_abortRequested) yield break; // could have been aborted during the wait above

                _tapReceived = false;
                if (!spec.IsCatchTrial)
                    _tonePlayer.PlayTone(spec.FrequencyHz, 0.16f, spec.Channel);

                float elapsed = 0f;
                while (elapsed < ActiveWindowSeconds && !_tapReceived)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                TrialOutcome outcome = spec.IsCatchTrial
                    ? (_tapReceived ? TrialOutcome.FalsePositive : TrialOutcome.CorrectRejection)
                    : (_tapReceived ? TrialOutcome.CorrectDetection : TrialOutcome.Miss);

                yield return StartCoroutine(ProcessTrial(outcome, spec.Channel, spec.FrequencyHz));
            }

            if (!_abortRequested)
                CompleteSession();
        }

        // Fisher-Yates, matching TrialPlan.BuildDefault's own shuffle - re-shuffled each time the
        // plan wraps so a long session's repeats don't read as an obviously looping pattern.
        private static void Shuffle(List<TrialSpec> plan)
        {
            for (int i = plan.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (plan[i], plan[j]) = (plan[j], plan[i]);
            }
        }

        /// <summary>
        /// Shared per-trial pipeline: record -> advance progress -> notify world -> wait for the
        /// world to report it is listening-safe again. Used by both the real loop above and the
        /// development mock driver, so both paths exercise identical engine behavior.
        /// </summary>
        /// <param name="frequencyHz">The reference frequency this trial tested, if any (0 for a
        /// dev-injected outcome with no real trial behind it) - only used to compute <see
        /// cref="LastAwardedPoints"/> on a CorrectDetection.</param>
        public IEnumerator ProcessTrial(TrialOutcome outcome, EarChannel channel, float frequencyHz = 0f)
        {
            CurrentResult.Record(outcome, channel, frequencyHz);
            // Progress itself now advances continuously in Update(), not here - see BeginSession.
            if (outcome == TrialOutcome.CorrectDetection)
                LastAwardedPoints = PointsForFrequency(frequencyHz);

            var ctx = new OutcomePresentationContext(outcome, channel, Progress);

            bool safe = false;
            void OnSafe() => safe = true;
            _world.ListeningSafe += OnSafe;

            _world.PresentOutcome(ctx);
            _world.SetSessionProgress(Progress);

            float waited = 0f;
            while (!safe && waited < ListeningSafeTimeoutSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            _world.ListeningSafe -= OnSafe;
        }

        /// <summary>Force-completes the session (progress -> 1.0) regardless of trial outcomes so far.</summary>
        public void CompleteSession()
        {
            IsRunning = false;
            Progress = 1f;
            _world.SetSessionProgress(1f);
            _world.CompleteSession(CurrentResult);
        }

        /// <summary>Placeholder scoring: scales continuously with the reference frequency (higher
        /// = more points, as a simple stand-in for "quiet/off-frequency tones are worth more"),
        /// plus a small random jitter so results don't all land on a round multiple of ten
        /// (human request 2026-09-26: "at je vysledek klidne 231 nebo 347"). This has no
        /// relationship to how audible the tone actually was at the volume it played - real
        /// difficulty-based scoring needs per-trial volume data the engine doesn't track yet.
        /// See roadmap 009.</summary>
        private static int PointsForFrequency(float hz)
        {
            const float minHz = 1000f, maxHz = 16000f;
            float t = Mathf.InverseLerp(minHz, maxHz, hz);
            int basePoints = Mathf.RoundToInt(Mathf.Lerp(5f, 22f, t));
            int jitter = UnityEngine.Random.Range(-3, 4);
            return Mathf.Clamp(basePoints + jitter, 1, 25);
        }
    }
}
