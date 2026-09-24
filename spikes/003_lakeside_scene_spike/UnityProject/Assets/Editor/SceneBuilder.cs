using System.IO;
using LakesideSceneSpike;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/LakesideScene.unity";

    [MenuItem("LakesideSpike/Build Scene")]
    public static void BuildScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var controllerObj = new GameObject("GameController");
        controllerObj.AddComponent<LakesideSceneController>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log($"[SceneBuilder] Scene built and saved at {ScenePath}");
    }
}
