using HearApp.Dev;
using UnityEditor;
using UnityEngine;

namespace HearApp.Editor
{
    /// <summary>
    /// Lets `-batchmode -executeMethod` runs actually execute <see cref="IntegrationProofRunner"/>
    /// headlessly (entering Play mode so coroutines/scene loads run) instead of requiring a human
    /// to click the DevOverlay button, so this milestone's key deliverable can be re-verified from
    /// the command line at any time.
    /// </summary>
    public static class HearAppIntegrationProofCli
    {
        private static GameObject _runnerObj;

        [MenuItem("Hear/Run Integration Proof")]
        public static void Run()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            _runnerObj = new GameObject("CliIntegrationProofRunner");
            var runner = _runnerObj.AddComponent<IntegrationProofRunner>();
            runner.StartCoroutine(RunAndExit(runner));
        }

        private static System.Collections.IEnumerator RunAndExit(IntegrationProofRunner runner)
        {
            bool sawFail = false;
            Application.logMessageReceived += Handler;
            void Handler(string condition, string trace, LogType type)
            {
                if (condition.Contains("[IntegrationProof] FAIL") || type == LogType.Error || type == LogType.Exception)
                    sawFail = true;
            }

            yield return runner.StartCoroutine(runner.RunProof());
            Application.logMessageReceived -= Handler;

            Debug.Log(sawFail ? "[HearAppIntegrationProofCli] RESULT=FAIL" : "[HearAppIntegrationProofCli] RESULT=PASS");
            EditorApplication.Exit(sawFail ? 1 : 0);
        }
    }
}
