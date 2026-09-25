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

        private IWorldPresentation _world;
        private TonePlayer _tonePlayer;
        private int _totalPlanned;
        private bool _tapReceived;
        private float _sessionElapsedSeconds;
        private float _estimatedTotalSeconds;

        private void Awake()
        {
            _tonePlayer = gameObject.AddComponent<TonePlayer>();
        }

        private void Update()
        {
            if (!IsRunning) return;
            // Time.timeScale == 0 is the Playing HUD's pause button (ShellUIController); a tap
            // landing while paused must not bleed into the trial that resumes.
            if (Time.timeScale > 0f && Pointer.current?.press.wasPressedThisFrame == true)
                _tapReceived = true;

            // Time-based, continuously advancing rather than jumping once per completed trial -
            // the discrete per-trial jump read as an unintended "tap now" cue on-device (human
            // feedback 2026-09-25: "jako kdyby dával impuls: klikni na obrazovku"). Capped below
            // 1 while running so it never *looks* finished before CompleteSession actually snaps
            // it to 1.
            _sessionElapsedSeconds += Time.deltaTime;
            Progress = Mathf.Min(0.97f, _sessionElapsedSeconds / Mathf.Max(0.01f, _estimatedTotalSeconds));
        }

        /// <summary>Attaches a world and resets session state. Call once per world entry.</summary>
        public void BeginSession(IWorldPresentation world, WorldContext context, int totalPlannedTrials)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            CurrentResult = new SessionResult();
            _totalPlanned = Mathf.Max(1, totalPlannedTrials);
            // Rough per-trial average (idle wait + a partial active window, since most trials end
            // early on a tap rather than running the full window) - only used to pace the
            // continuous progress bar, not for anything measurement-accurate.
            _estimatedTotalSeconds = _totalPlanned * ((MinIdleSeconds + MaxIdleSeconds) / 2f + ActiveWindowSeconds * 0.5f);
            _sessionElapsedSeconds = 0f;
            Progress = 0f;
            IsRunning = true;
            _world.Initialize(context);
        }

        /// <summary>Runs the real audio-driven trial loop against the given plan.</summary>
        public IEnumerator RunSession(List<TrialSpec> plan)
        {
            foreach (var spec in plan)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(MinIdleSeconds, MaxIdleSeconds));

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

            CompleteSession();
        }

        /// <summary>
        /// Shared per-trial pipeline: record -> advance progress -> notify world -> wait for the
        /// world to report it is listening-safe again. Used by both the real loop above and the
        /// development mock driver, so both paths exercise identical engine behavior.
        /// </summary>
        public IEnumerator ProcessTrial(TrialOutcome outcome, EarChannel channel, float frequencyHz = 0f)
        {
            CurrentResult.Record(outcome, channel, frequencyHz);
            // Progress itself now advances continuously in Update(), not here - see BeginSession.

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
    }
}
