using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildSpike2
{
    public static void BuildAndroidEnergetic()
    {
        SceneBuilder.BuildEnergeticScene();

        PlayerSettings.companyName = "Jan Zeman";
        PlayerSettings.productName = "002 Energetic";
        PlayerSettings.applicationIdentifier = "com.janzeman.hear.spike002.energetic";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        string outputDir = Path.Combine(Application.dataPath, "..", "Build");
        Directory.CreateDirectory(outputDir);
        string apkPath = Path.Combine(outputDir, "UiFeelSpikeEnergetic.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/EnergeticScene.unity" },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildSpike2] Building Energetic Android APK to {apkPath} ...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildSpike2] Build result: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}, size: {report.summary.totalSize} bytes");

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void BuildAndroidDusk()
    {
        SceneBuilder.BuildDuskScene();

        PlayerSettings.companyName = "Jan Zeman";
        PlayerSettings.productName = "002 Dusk";
        PlayerSettings.applicationIdentifier = "com.janzeman.hear.spike002.dusk";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        string outputDir = Path.Combine(Application.dataPath, "..", "Build");
        Directory.CreateDirectory(outputDir);
        string apkPath = Path.Combine(outputDir, "UiFeelSpikeDusk.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/DuskScene.unity" },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildSpike2] Building Dusk Android APK to {apkPath} ...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildSpike2] Build result: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}, size: {report.summary.totalSize} bytes");

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
