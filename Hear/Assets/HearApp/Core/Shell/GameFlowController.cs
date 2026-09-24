using System;
using System.Collections;
using System.Collections.Generic;
using HearApp.Core.HearingEngine;
using HearApp.Core.Worlds;
using UnityEngine;
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

        public ShellState State { get; private set; } = ShellState.Splash;
        public int SelectedWorldIndex { get; private set; }
        public AudioOutputMode OutputMode { get; private set; } = AudioOutputMode.Speaker;
        public TrialEngine Engine { get; private set; }

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

        /// <summary>Player tapped Play on the World Selector for the currently selected world.</summary>
        public void RequestPlaySelectedWorld() => SetState(ShellState.HeadphoneChoice);

        /// <summary>Player picked Headphones or Speaker on the (never-blocking) headphone-choice screen.</summary>
        public void ChooseOutputMode(AudioOutputMode mode)
        {
            OutputMode = mode;
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
            Engine.BeginSession(_activeWorld, context, plan.Count);

            SetState(ShellState.Playing);
            yield return StartCoroutine(Engine.RunSession(plan));

            SessionCompleted?.Invoke(Engine.CurrentResult);
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
        public Coroutine InjectDevOutcome(TrialOutcome outcome, EarChannel channel)
        {
            if (_activeWorld == null || !Engine.IsRunning) return null;
            return StartCoroutine(Engine.ProcessTrial(outcome, channel));
        }

        public void ReturnToSelectorFromResults()
        {
            if (_loadedWorldScene.IsValid())
                SceneManager.UnloadSceneAsync(_loadedWorldScene);
            SetState(ShellState.WorldSelector);
        }

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
