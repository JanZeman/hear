using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

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
            EnsureGltfShadersIncluded();

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

        // glTFast's Shader Graph shaders (used by the River of Echoes canoeist model, a runtime-
        // loaded glTF) render as solid magenta unless explicitly kept out of build-time shader
        // stripping. Hand-editing ProjectSettings/GraphicsSettings.asset's guid list directly did
        // not take effect (found 2026-09-26), so this instead loads the actual Shader assets and
        // adds them via SerializedObject - the same mechanism the "Always Included Shaders"
        // Inspector list uses, guaranteed to encode the right fileID/guid/type.
        private static void EnsureGltfShadersIncluded()
        {
            var shaderPaths = new[]
            {
                "Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-pbrMetallicRoughness.shadergraph",
                "Packages/com.unity.cloud.gltfast/Runtime/Shader/glTF-unlit.shadergraph",
            };

            var graphicsSettingsObject = GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(graphicsSettingsObject);
            var shadersProp = so.FindProperty("m_AlwaysIncludedShaders");

            foreach (var path in shaderPaths)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null)
                {
                    Debug.LogWarning($"[HearDevelopmentBuild] Could not load shader at '{path}' to always-include it.");
                    continue;
                }

                bool alreadyIncluded = false;
                for (int i = 0; i < shadersProp.arraySize; i++)
                {
                    if (shadersProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        alreadyIncluded = true;
                        break;
                    }
                }
                if (alreadyIncluded) continue;

                shadersProp.InsertArrayElementAtIndex(shadersProp.arraySize);
                shadersProp.GetArrayElementAtIndex(shadersProp.arraySize - 1).objectReferenceValue = shader;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
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
                    // Jan's active Apple Developer Program team
                    PlayerSettings.iOS.appleDeveloperTeamID = "9VBQGD32YX";
                    break;

                case BuildTarget.StandaloneOSX:
                    PlayerSettings.SetApplicationIdentifier(
                        NamedBuildTarget.Standalone,
                        "com.janzeman.hear");
                    PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                    // Small and narrow-ish on purpose (human request 2026-09-26: the previous
                    // default filled a 27" monitor's whole height) and roughly portrait, matching
                    // this app's actual mobile presentation instead of an arbitrary landscape size.
                    PlayerSettings.defaultScreenWidth = 480;
                    PlayerSettings.defaultScreenHeight = 854;
                    PlayerSettings.resizableWindow = true;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(target), target, null);
            }
        }
    }
}
