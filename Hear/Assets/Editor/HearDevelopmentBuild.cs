using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hear.Editor
{
    public static class HearDevelopmentBuild
    {
        private const string AndroidOutput = "Builds/Android/Hear.apk";
        private const string IosOutput = "Builds/iOS";
        private const string MacOsOutput = "Builds/macOS/Hear.app";

        private const string AndroidReleaseOutput = "Builds/Android/HearRelease.apk";

        [MenuItem("Hear/Build Development/Android")]
        public static void BuildAndroid()
        {
            Build(BuildTarget.Android, AndroidOutput);
        }

        [MenuItem("Hear/Build Development/Android and Run")]
        public static void BuildAndRunAndroid()
        {
            Build(BuildTarget.Android, AndroidOutput, BuildOptions.AutoRunPlayer);
        }

        // Non-development Android build: excludes DevOverlay (#if DEVELOPMENT_BUILD) and the
        // "Development Build" watermark, for visual QA that must match what actually ships.
        [MenuItem("Hear/Build Release/Android")]
        public static void BuildAndroidRelease()
        {
            Build(BuildTarget.Android, AndroidReleaseOutput, BuildOptions.None, development: false);
        }

        [MenuItem("Hear/Build Development/iOS")]
        public static void BuildIos()
        {
            Build(BuildTarget.iOS, IosOutput);
        }

        [MenuItem("Hear/Build Development/macOS")]
        public static void BuildMacOs()
        {
            Build(BuildTarget.StandaloneOSX, MacOsOutput);
        }

        [MenuItem("Hear/Build Development/macOS and Run")]
        public static void BuildAndRunMacOs()
        {
            Build(BuildTarget.StandaloneOSX, MacOsOutput, BuildOptions.AutoRunPlayer);
        }

        private static void Build(
            BuildTarget target,
            string relativeOutputPath,
            BuildOptions additionalOptions = BuildOptions.None,
            bool development = true)
        {
            ConfigurePlayerSettings(target);

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for the build.");
            }

            var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativeOutputPath));
            var outputDirectory = target == BuildTarget.iOS
                ? outputPath
                : Path.GetDirectoryName(outputPath);
            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = (development ? BuildOptions.Development : BuildOptions.None) | additionalOptions
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Hear {target} build failed with {report.summary.totalErrors} error(s).");
            }

            Debug.Log($"Hear {target} development build created at {outputPath}.");
        }

        private static void ConfigurePlayerSettings(BuildTarget target)
        {
            PlayerSettings.productName = "Hear";

            switch (target)
            {
                case BuildTarget.Android:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.Android,
                        "com.janzeman.hear");
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                    PlayerSettings.SetScriptingBackend(
                        NamedBuildTarget.Android,
                        ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    // Per human direction 2026-09-25: the design keeps the OS status bar visible
                    // (unlike the OS nav bar). The previous "start in fullscreen" setting hides
                    // both natively before any of our own C# runs, and a runtime override
                    // (GameFlowController.ApplyAndroidStatusBarVisibleNavHidden) was not enough to
                    // keep it un-hidden - disabling native fullscreen here so our runtime code is
                    // the only thing controlling bar visibility, instead of fighting Unity's own
                    // startup behavior.
                    PlayerSettings.Android.startInFullscreen = false;
                    break;

                case BuildTarget.iOS:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.iOS,
                        "com.janzeman.hear");
                    PlayerSettings.iOS.targetOSVersionString = "15.0";
                    break;

                case BuildTarget.StandaloneOSX:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.Standalone,
                        "com.janzeman.hear");
                    PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                    PlayerSettings.defaultScreenWidth = 1280;
                    PlayerSettings.defaultScreenHeight = 800;
                    PlayerSettings.resizableWindow = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }
        }
    }
}
