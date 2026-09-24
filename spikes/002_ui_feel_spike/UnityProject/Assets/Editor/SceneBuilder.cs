using System.IO;
using UiFeelSpike.Dusk;
using UiFeelSpike.Energetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneBuilder
{
    private const string EnergeticScenePath = "Assets/Scenes/EnergeticScene.unity";
    private const string DuskScenePath = "Assets/Scenes/DuskScene.unity";

    [MenuItem("UiFeelSpike/Build Energetic Scene")]
    public static void BuildEnergeticScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var controllerObj = new GameObject("GameController");
        controllerObj.AddComponent<EnergeticBirdController>();

        EditorSceneManager.SaveScene(scene, EnergeticScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(EnergeticScenePath, true) };

        Debug.Log($"[SceneBuilder] Energetic scene built and saved at {EnergeticScenePath}");
    }

    [MenuItem("UiFeelSpike/Build Dusk Scene")]
    public static void BuildDuskScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var controllerObj = new GameObject("GameController");
        controllerObj.AddComponent<DuskLakeFireflyController>();

        EditorSceneManager.SaveScene(scene, DuskScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DuskScenePath, true) };

        Debug.Log($"[SceneBuilder] Dusk scene built and saved at {DuskScenePath}");
    }
}
