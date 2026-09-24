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
            BuildOptions additionalOptions = BuildOptions.None)
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
                options = BuildOptions.Development | additionalOptions
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
                    break;

                case BuildTarget.iOS:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.iOS,
                        "com.janzeman.hear");
                    break;

                case BuildTarget.StandaloneOSX:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.Standalone,
                        "com.janzeman.hear");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }
        }
    }
}
