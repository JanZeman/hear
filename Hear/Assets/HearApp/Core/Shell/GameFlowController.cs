using System;
using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Results;
using HearApp.Core.Worlds;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HearApp.Core.Shell
{
    /// <summary>
    /// Owns navigation/state for the whole app: splash -> world selector -> headphone/speaker
    /// choice -> micro-instruction -> play -> results, plus additively loading/unloading the
    /// selected world's scene and wiring it to the (single, persistent) <see cref="TrialEngine"/>.
    /// This is Game Shell responsibility per docs/06 - it never touches trial timing or
    /// classification itself, only asks the engine to run a plan against whichever world is loaded.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        public enum ShellState
        {
            Splash,
            WorldSelector,
            HeadphoneChoice,
            MicroInstruction,
            Playing,
            Results,
            Settings
        }

        public static GameFlowController Instance { get; private set; }

        public event Action<ShellState> StateChanged;
        public event Action<int> SelectedWorldIndexChanged;
        public event Action<SessionResult> SessionCompleted;

        // Dev-speed settings (primitive Settings screen, human request 2026-09-25): skip the
        // never-blocking headphone-choice and micro-instruction screens so starting a session
        // drops straight into play. PlayerPrefs-backed, default true (both skipped) since the
        // request was specifically "abychom to mohli rychleji testovat".
        private const string SkipHeadphoneChoiceKey = "Settings.SkipHeadphoneChoice";
        private const string SkipMicroInstructionKey = "Settings.SkipMicroInstruction";
        private const string LongSessionKey = "Settings.LongSession";

        public bool SkipHeadphoneChoice
        {
            get => PlayerPrefs.GetInt(SkipHeadphoneChoiceKey, 1) == 1;
            set { PlayerPrefs.SetInt(SkipHeadphoneChoiceKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public bool SkipMicroInstruction
        {
            get => PlayerPrefs.GetInt(SkipMicroInstructionKey, 1) == 1;
            set { PlayerPrefs.SetInt(SkipMicroInstructionKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Runs a 10x longer session (human request 2026-09-25: re-triggering the whole
        /// shell flow every ~30s was slowing down playtesting a world). Remove once the real
        /// worlds don't need this much iteration anymore.</summary>
        public bool LongSession
        {
            get => PlayerPrefs.GetInt(LongSessionKey, 1) == 1;
            set { PlayerPrefs.SetInt(LongSessionKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Every world's session runs for exactly this long (human request 2026-09-26:
        /// "at v jakemkoli svete tvrva presne 30 sekund") - not a trial count, a wall-clock
        /// target the engine cycles trials against (see TrialEngine.RunSession).</summary>
        private const float SessionDurationSeconds = 30f;

        public ShellState State { get; private set; } = ShellState.Splash;
        public int SelectedWorldIndex { get; private set; }
        public AudioOutputMode OutputMode { get; private set; } = AudioOutputMode.Speaker;
        public TrialEngine Engine { get; private set; }

        /// <summary>True when the Results state was entered by just finishing a session (so the
        /// Results screen should show the post-session context); false when entered via the
        /// bottom nav's Results tab (the neutral "Overall Results" context). Set in
        /// <see cref="EnterWorldRoutine"/> / <see cref="ViewOverallResults"/> - the only two paths
        /// into <see cref="ShellState.Results"/>.</summary>
        public bool HasJustCompletedSession { get; private set; }

        private Scene _loadedWorldScene;
        private WorldPresentationBase _activeWorld;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Unity's built-in URP Rendering Debugger runtime overlay - not something this app
            // uses, but it ships enabled in Development builds and can pop up over the game
            // (human report 2026-09-25: "ta debug screen... trochu mi překáží").
            if (DebugManager.instance != null)
                DebugManager.instance.enableRuntimeUI = false;

#if UNITY_ANDROID && !UNITY_EDITOR
            ApplyAndroidStatusBarVisibleNavHidden();
#endif

            Engine = gameObject.AddComponent<TrialEngine>();

            // Cold-start: randomize the initially active carousel item without reordering the
            // stable WorldRegistry list itself.
            SelectedWorldIndex = UnityEngine.Random.Range(0, WorldRegistry.Worlds.Count);
        }

        private void Start()
        {
            StartCoroutine(SplashThenSelector());
        }

        private IEnumerator SplashThenSelector()
        {
            SetState(ShellState.Splash);
            yield return new WaitForSeconds(0.8f);
            SetState(ShellState.WorldSelector);
        }

        public void SelectWorld(int index)
        {
            if (index < 0 || index >= WorldRegistry.Worlds.Count) return;
            SelectedWorldIndex = index;
            SelectedWorldIndexChanged?.Invoke(index);
        }

        /// <summary>Player tapped Play (or the active carousel card) for the currently selected world.</summary>
        public void RequestPlaySelectedWorld()
        {
            if (!SkipHeadphoneChoice)
            {
                SetState(ShellState.HeadphoneChoice);
                return;
            }
            // Headphone choice skipped: OutputMode keeps its existing/default value (Speaker)
            // rather than prompting for it.
            if (SkipMicroInstruction)
                StartCoroutine(EnterWorldRoutine());
            else
                SetState(ShellState.MicroInstruction);
        }

        /// <summary>Player picked Headphones or Speaker on the (never-blocking) headphone-choice screen.</summary>
        public void ChooseOutputMode(AudioOutputMode mode)
        {
            OutputMode = mode;
            if (SkipMicroInstruction)
                StartCoroutine(EnterWorldRoutine());
            else
                SetState(ShellState.MicroInstruction);
        }

        /// <summary>Player dismissed/demonstrated the micro-instruction; enter the world and start the session.</summary>
        public void ConfirmMicroInstruction() => StartCoroutine(EnterWorldRoutine());

        private IEnumerator EnterWorldRoutine()
        {
            var entry = WorldRegistry.Worlds[SelectedWorldIndex];
            var loadOp = SceneManager.LoadSceneAsync(entry.SceneName, LoadSceneMode.Additive);
            yield return loadOp;

            _loadedWorldScene = SceneManager.GetSceneByName(entry.SceneName);
            _activeWorld = FindWorldPresentation(_loadedWorldScene);
            if (_activeWorld == null)
            {
                Debug.LogError($"[GameFlowController] World scene '{entry.SceneName}' has no WorldPresentationBase.");
                SetState(ShellState.WorldSelector);
                yield break;
            }

            var context = new WorldContext(OutputMode, Environment.TickCount);
            var plan = TrialPlan.BuildDefault(OutputMode, new System.Random());
            float targetDuration = LongSession ? SessionDurationSeconds * 10f : SessionDurationSeconds;
            Engine.BeginSession(_activeWorld, context, targetDuration);

            SetState(ShellState.Playing);
            yield return StartCoroutine(Engine.RunSession(plan));

            // A deliberate "Quit to Home" (ShellUIController's pause menu) already unloaded the
            // scene and moved to WorldSelector directly, synchronously, before RunSession's own
            // loop got a chance to notice the abort flag and yield-break here - without this
            // check, this coroutine would then overwrite that with SetState(Results) a frame or
            // two later (a jarring flash back to Results after already leaving).
            if (Engine.SessionEndedByUserQuit) yield break;

            // Frantic-tap-invalidated sessions are explicitly "not counted" (human request
            // 2026-09-26 - see ShellUIController.ShowResultsScreen) - don't persist them into
            // history either, only real/normal completions.
            if (!Engine.SessionInvalidatedByFranticTapping)
            {
                var record = SessionHistoryEntry.FromResult(entry.Id, Engine.CurrentResult);
                SessionHistoryStore.Append(record);
            }

            HasJustCompletedSession = true;
            SessionCompleted?.Invoke(Engine.CurrentResult);
            SetState(ShellState.Results);
        }

        /// <summary>Bottom nav's Results tab: always shows the neutral "Overall Results" context,
        /// even if the player's last session is still fresh.</summary>
        public void ViewOverallResults()
        {
            HasJustCompletedSession = false;
            SetState(ShellState.Results);
        }

        /// <summary>Dev-only shortcut: skip splash/selector/headphone-choice/instruction and jump straight
        /// into a world session, so world presentation can be iterated on without replaying the whole shell flow.</summary>
        public void StartDevSession(int worldIndex, AudioOutputMode mode)
        {
            if (_loadedWorldScene.IsValid())
                SceneManager.UnloadSceneAsync(_loadedWorldScene);
            SelectWorld(worldIndex);
            OutputMode = mode;
            StartCoroutine(EnterWorldRoutine());
        }

        /// <summary>Dev-only: inject a classified outcome directly into the running session's active world,
        /// bypassing real audio scheduling/timing entirely. No-op if no session is running.</summary>
        public Coroutine InjectDevOutcome(TrialOutcome outcome, EarChannel channel, float frequencyHz = 1000f)
        {
            if (_activeWorld == null || !Engine.IsRunning) return null;
            return StartCoroutine(Engine.ProcessTrial(outcome, channel, frequencyHz));
        }

        public void ReturnToSelectorFromResults()
        {
            if (_loadedWorldScene.IsValid())
                SceneManager.UnloadSceneAsync(_loadedWorldScene);
            SetState(ShellState.WorldSelector);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnApplicationFocus(bool hasFocus)
        {
            // The deprecated-but-still-functional systemUiVisibility flags get cleared by Android
            // whenever the app loses focus (e.g. a swipe near the edge, an interruption) and must
            // be reapplied on refocus, or the OS nav bar reappears permanently.
            if (hasFocus) ApplyAndroidStatusBarVisibleNavHidden();
        }

        /// <summary>
        /// Per human direction 2026-09-25: the reference design keeps the OS status bar visible
        /// (its icons "left lit"), matching this device's normal behavior - only the bottom OS
        /// navigation bar should be immersive-hidden, not both (PlayerSettings' androidFullscreenMode
        /// hides both, which is wrong here). Uses the older View.setSystemUiVisibility flags rather
        /// than WindowInsetsController - functional through current Android versions and avoids
        /// adding an AndroidX Core Gradle dependency for this one call.
        /// </summary>
        private void ApplyAndroidStatusBarVisibleNavHidden()
        {
            try
            {
                // `using` here would dispose these the instant this method returns - but
                // runOnUiThread only *posts* the runnable, it does not block until it runs, so the
                // captured `activity` was already invalid by the time the runnable actually
                // executed (confirmed via a NullReferenceException inside the runnable in logcat).
                // Not disposed here; the JNI global refs are cleaned up by the finalizer, which is
                // fine for a call made only once per focus event.
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using var window = activity.Call<AndroidJavaObject>("getWindow");

                    const int WindowFlagFullscreen = 0x00000400; // WindowManager.LayoutParams.FLAG_FULLSCREEN - would hide the status bar; must stay cleared.
                    const int WindowFlagDrawsSystemBarBackgrounds = unchecked((int)0x80000000);
                    window.Call("clearFlags", WindowFlagFullscreen);
                    window.Call("addFlags", WindowFlagDrawsSystemBarBackgrounds);
                    window.Call("setStatusBarColor", 0); // transparent, so our own art shows through behind it

                    using var decorView = window.Call<AndroidJavaObject>("getDecorView");
                    const int SystemUiFlagLayoutStable = 0x00000100;
                    const int SystemUiFlagLayoutFullscreen = 0x00000400; // content draws under the (still-visible) status bar - not the hide-it flag (SYSTEM_UI_FLAG_FULLSCREEN, 0x00000004).
                    const int SystemUiFlagLayoutHideNavigation = 0x00000200;
                    const int SystemUiFlagHideNavigation = 0x00000002;
                    const int SystemUiFlagImmersiveSticky = 0x00001000;
                    int flags = SystemUiFlagLayoutStable | SystemUiFlagLayoutFullscreen
                        | SystemUiFlagLayoutHideNavigation | SystemUiFlagHideNavigation | SystemUiFlagImmersiveSticky;
                    decorView.Call("setSystemUiVisibility", flags);
                }));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameFlowController] Could not apply Android status-bar-visible/nav-hidden window flags: {e}");
            }
        }
#endif

        private static WorldPresentationBase FindWorldPresentation(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<WorldPresentationBase>(true);
                if (found != null) return found;
            }
            return null;
        }

        private void SetState(ShellState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
