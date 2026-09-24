using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildSpike3
{
    private const string ScenePath = "Assets/Scenes/LakesideScene.unity";

    public static void BuildAndroid()
    {
        SceneBuilder.BuildScene();

        PlayerSettings.companyName = "Jan Zeman";
        PlayerSettings.productName = "003 Lakeside";
        PlayerSettings.applicationIdentifier = "com.janzeman.hear.spike003";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        string outputDir = Path.Combine(Application.dataPath, "..", "Build");
        Directory.CreateDirectory(outputDir);
        string apkPath = Path.Combine(outputDir, "LakesideSpike.apk");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildSpike3] Building Android APK to {apkPath} ...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildSpike3] Build result: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}, size: {report.summary.totalSize} bytes");

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void BuildMacOS()
    {
        SceneBuilder.BuildScene();

        PlayerSettings.companyName = "Jan Zeman";
        PlayerSettings.productName = "003 Lakeside";
        PlayerSettings.applicationIdentifier = "com.janzeman.hear.spike003";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.displayResolutionDialog = ResolutionDialogSetting.Disabled;

        string outputDir = Path.Combine(Application.dataPath, "..", "Build", "macOS");
        Directory.CreateDirectory(outputDir);
        string appPath = Path.Combine(outputDir, "LakesideSpike.app");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = appPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        Debug.Log($"[BuildSpike3] Building macOS app to {appPath} ...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[BuildSpike3] Build result: {report.summary.result}, errors: {report.summary.totalErrors}, warnings: {report.summary.totalWarnings}, size: {report.summary.totalSize} bytes");

        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
