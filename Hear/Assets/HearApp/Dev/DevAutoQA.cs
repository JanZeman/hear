using System;
using System.Collections;
using System.IO;
using HearApp.Core.Shell;
using HearApp.Core.Worlds;
using UnityEngine;

namespace HearApp.Dev
{
    /// <summary>
    /// Local visual-QA aid for iterating on a world's look without a physical device: only
    /// activates in a macOS Standalone dev build launched with both
    /// `-devAutoQAWorld=&lt;worldId&gt;` and `-devAutoQAOutDir=&lt;absolute path&gt;` on the command
    /// line, in which case it auto-navigates straight into that world (skipping the selector,
    /// headphone choice, and micro-instruction screens) and dumps timed screenshots of the running
    /// session to disk via Unity's own <see cref="ScreenCapture"/> API, then quits. This needs no
    /// macOS Screen Recording permission (unlike an OS-level screen grab) since it captures the
    /// app's own backbuffer. Inert - and compiled out of every non-macOS-Standalone target - so it
    /// can never affect a real device build.
    /// </summary>
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
    public sealed class DevAutoQA : MonoBehaviour
    {
        private static readonly float[] ShotTimesSeconds = { 1f, 4f, 9f, 15f, 21f, 27f, 29.5f };

        private string _worldId;
        private string _outDir;
        private bool _navigated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            string worldId = GetArg("-devAutoQAWorld");
            string outDir = GetArg("-devAutoQAOutDir");
            if (string.IsNullOrEmpty(worldId) || string.IsNullOrEmpty(outDir)) return;

            // Launched as a background process (no window focus), Unity otherwise throttles/pauses
            // Update() entirely while unfocused - the first run just sat frozen at startup with no
            // further log activity (found 2026-09-26). This keeps the loop running regardless.
            Application.runInBackground = true;

            var go = new GameObject("DevAutoQA");
            UnityEngine.Object.DontDestroyOnLoad(go);
            // AddComponent<T>() runs Awake() synchronously before returning, so fields set on the
            // reference afterward arrive too late for Awake() to see them - use an explicit Init()
            // instead (found via Player.log: ArgumentNullException, CreateDirectory(null), 2026-09-26).
            var qa = go.AddComponent<DevAutoQA>();
            qa.Init(worldId, outDir);
        }

        private void Init(string worldId, string outDir)
        {
            _worldId = worldId;
            _outDir = outDir;
            Directory.CreateDirectory(_outDir);
        }

        private void Update()
        {
            if (_navigated) return;
            var flow = GameFlowController.Instance;
            if (flow == null || flow.State != GameFlowController.ShellState.WorldSelector) return;

            int index = -1;
            for (int i = 0; i < WorldRegistry.Worlds.Count; i++)
                if (WorldRegistry.Worlds[i].Id == _worldId) { index = i; break; }
            if (index < 0)
            {
                Debug.LogError($"[DevAutoQA] Unknown world id '{_worldId}'.");
                Application.Quit();
                return;
            }

            flow.SelectWorld(index);
            flow.RequestPlaySelectedWorld();
            _navigated = true;
            StartCoroutine(CaptureRoutine(flow));
        }

        private IEnumerator CaptureRoutine(GameFlowController flow)
        {
            float safety = 0f;
            while (flow.State != GameFlowController.ShellState.Playing && safety < 10f)
            {
                safety += Time.unscaledDeltaTime;
                yield return null;
            }

            float sessionStart = Time.time;
            foreach (var t in ShotTimesSeconds)
            {
                while (Time.time - sessionStart < t)
                    yield return null;
                string path = Path.Combine(_outDir, $"shot_{t:00.0}s.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[DevAutoQA] Captured {path}");
                yield return new WaitForSeconds(0.2f); // let the capture flush before continuing
            }

            yield return new WaitForSeconds(1f);
            Debug.Log("[DevAutoQA] Done, quitting.");
            Application.Quit();
        }

        private static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            foreach (var a in args)
                if (a.StartsWith(name + "=", StringComparison.Ordinal))
                    return a.Substring(name.Length + 1);
            return null;
        }
    }
#endif
}
