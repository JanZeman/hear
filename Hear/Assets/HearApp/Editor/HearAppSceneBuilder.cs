using System.IO;
using HearApp.Core.Shell;
using HearApp.Core.Shell.UI;
using HearApp.Worlds.PaperGarden;
using HearApp.Worlds.RiverJourney;
using HearApp.Worlds.TideTroubles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using HearApp.Dev;
#endif

namespace HearApp.Editor
{
    /// <summary>
    /// Builds the persistent Bootstrap scene (GameFlowController + ShellUIController + dev
    /// overlay) and the three additive world scenes. Scenes are constructed procedurally so their
    /// composition remains reproducible and reviewable in source control.
    /// </summary>
    public static class HearAppSceneBuilder
    {
        private const string ScenesFolder = "Assets/HearApp/Scenes";
        private const string BootstrapScenePath = ScenesFolder + "/Bootstrap.unity";
        private const string TideTroublesScenePath = ScenesFolder + "/TideTroublesWorld.unity";
        private const string PaperGardenScenePath = ScenesFolder + "/PaperGardenWorld.unity";
        private const string RiverJourneyScenePath = ScenesFolder + "/RiverJourneyWorld.unity";

        [MenuItem("Hear/Build All Scenes")]
        public static void BuildAllScenes()
        {
            Directory.CreateDirectory(ScenesFolder);

            BuildBootstrapScene();
            BuildWorldScene<TideTroublesPresentation>(TideTroublesScenePath, "TideTroublesRoot");
            BuildWorldScene<PaperGardenPresentation>(PaperGardenScenePath, "PaperGardenRoot");
            BuildWorldScene<RiverJourneyPresentation>(RiverJourneyScenePath, "RiverJourneyRoot");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapScenePath, true),
                new EditorBuildSettingsScene(TideTroublesScenePath, true),
                new EditorBuildSettingsScene(PaperGardenScenePath, true),
                new EditorBuildSettingsScene(RiverJourneyScenePath, true),
            };

            Debug.Log("[HearAppSceneBuilder] Built Bootstrap and three world scenes and registered them in Build Settings.");
        }

        private static void BuildBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var flowObj = new GameObject("GameFlowController");
            flowObj.AddComponent<GameFlowController>();
            flowObj.AddComponent<MinimumWindowSizeEnforcer>();

            var uiObj = new GameObject("ShellUI");
            uiObj.AddComponent<UIDocument>();
            uiObj.AddComponent<ShellUIController>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var devObj = new GameObject("DevOverlay");
            devObj.AddComponent<DevOverlay>();
#endif

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            Debug.Log($"[HearAppSceneBuilder] Bootstrap scene saved at {BootstrapScenePath}");
        }

        private static void BuildWorldScene<T>(string path, string rootName) where T : Component
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject(rootName);
            root.AddComponent<T>();

            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[HearAppSceneBuilder] World scene saved at {path}");
        }
    }
}
