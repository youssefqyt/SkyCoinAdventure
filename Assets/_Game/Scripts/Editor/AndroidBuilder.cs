using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SkyCoin.EditorTools
{
    /// <summary>
    /// Command line Android build. There is deliberately NO menu item: the project is built
    /// from a terminal (or from the Unity command line) so the Editor stays clean.
    ///
    /// Usage (Unity closed, Android Build Support installed):
    ///
    ///   "C:\Program Files\Unity\Hub\Editor\6000.4.4f1\Editor\Unity.exe" ^
    ///     -batchmode -quit -projectPath "C:\Users\Dell\Desktop\Gamev" ^
    ///     -executeMethod SkyCoin.EditorTools.AndroidBuilder.BuildApk ^
    ///     -logFile "C:\Users\Dell\Desktop\Gamev\Builds\Android\build.log"
    ///
    /// Output: Builds/Android/SkyCoinAdventure.apk
    /// Exit code 0 = success, 1 = failure (so a script can detect it).
    /// </summary>
    public static class AndroidBuilder
    {
        private const string ApkFileName = "SkyCoinAdventure.apk";

        // MainMenu MUST be first: the game boots into scene 0 and returns to it by name.
        private static readonly string[] GameScenes =
        {
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/Level_01.unity",
            "Assets/_Game/Scenes/Level_02.unity"
        };

        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError(
                    "[AndroidBuilder] Android Build Support is NOT installed for this Unity editor.\n" +
                    "Install it from Unity Hub: Installs -> 6000.4.4f1 -> gear -> Add modules -> " +
                    "Android Build Support (+ OpenJDK). Then run this command again.");
                EditorApplication.Exit(1);
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Builds", "Android");
            Directory.CreateDirectory(outputDirectory);
            string apkPath = Path.Combine(outputDirectory, ApkFileName);

            ConfigureExternalTools();
            ConfigurePlayer();
            string[] scenes = ConfigureBuildScenes(projectRoot);
            if (scenes == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidBuilder] Switching active build target to Android...");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[AndroidBuilder] Could not switch to Android. Is the module installed correctly?");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            EditorUserBuildSettings.buildAppBundle = false;   // APK, not AAB

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuilder] Building APK -> {apkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == BuildResult.Succeeded)
            {
                double megabytes = report.summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[AndroidBuilder] SUCCESS: {apkPath} ({megabytes:0.0} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AndroidBuilder] FAILED: {report.summary.result} " +
                               $"({report.summary.totalErrors} error(s)). See the log above.");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Points Unity at the Android SDK / NDK / JDK that already exist on this machine, so
        /// the Hub does not have to download them again.
        /// </summary>
        private static void ConfigureExternalTools()
        {
            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty;

            string sdk = FirstExisting(
                Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                Environment.GetEnvironmentVariable("ANDROID_HOME"),
                Path.Combine(localAppData, "Android", "Sdk"));

            string ndk = null;
            if (!string.IsNullOrEmpty(sdk))
            {
                string ndkRoot = Path.Combine(sdk, "ndk");
                if (Directory.Exists(ndkRoot))
                {
                    // Highest installed version wins (27.1.12297006 on this machine).
                    string[] versions = Directory.GetDirectories(ndkRoot);
                    Array.Sort(versions, StringComparer.OrdinalIgnoreCase);
                    if (versions.Length > 0) ndk = versions[versions.Length - 1];
                }
            }

            // Prefer the OpenJDK shipped with the Android module; fall back to a local JDK.
            string jdk = FirstExisting(
                Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "OpenJDK"),
                Environment.GetEnvironmentVariable("JAVA_HOME"),
                FindAdoptiumJdk(localAppData));

            if (!string.IsNullOrEmpty(sdk))
            {
                EditorPrefs.SetString("AndroidSdkRoot", sdk);
                Environment.SetEnvironmentVariable("ANDROID_HOME", sdk);
                Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdk);
                Debug.Log($"[AndroidBuilder] SDK: {sdk}");
            }
            else
            {
                Debug.LogWarning("[AndroidBuilder] No Android SDK found. Unity will ask for one.");
            }

            if (!string.IsNullOrEmpty(ndk))
            {
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndk);
                Debug.Log($"[AndroidBuilder] NDK: {ndk}");
            }
            else
            {
                Debug.LogWarning("[AndroidBuilder] No Android NDK found under <sdk>/ndk.");
            }

            if (!string.IsNullOrEmpty(jdk))
            {
                EditorPrefs.SetString("JdkPath", jdk);
                Environment.SetEnvironmentVariable("JAVA_HOME", jdk);
                Debug.Log($"[AndroidBuilder] JDK: {jdk}");
            }
            else
            {
                Debug.LogWarning("[AndroidBuilder] No JDK found. Install OpenJDK from Unity Hub.");
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.skycoin.adventure");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Landscape only, both directions.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        /// <summary>
        /// Builds the scene list with MainMenu first and drops SampleScene. The result is also
        /// written back to EditorBuildSettings, so the Build Profiles window shows the same list.
        /// </summary>
        private static string[] ConfigureBuildScenes(string projectRoot)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            List<string> paths = new List<string>();

            foreach (string scene in GameScenes)
            {
                if (!File.Exists(Path.Combine(projectRoot, scene)))
                {
                    Debug.LogWarning($"[AndroidBuilder] Scene missing, skipped: {scene}");
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(scene, true));
                paths.Add(scene);
            }

            if (scenes.Count == 0)
            {
                Debug.LogError("[AndroidBuilder] None of the game scenes were found. Nothing to build.");
                return null;
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[AndroidBuilder] Scenes: {string.Join(", ", paths)}");
            return paths.ToArray();
        }

        private static string FirstExisting(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate)) return candidate;
            }

            return null;
        }

        private static string FindAdoptiumJdk(string localAppData)
        {
            string root = Path.Combine(localAppData, "Programs", "Eclipse Adoptium");
            if (!Directory.Exists(root)) return null;

            string[] candidates = Directory.GetDirectories(root, "jdk-*");
            Array.Sort(candidates, StringComparer.OrdinalIgnoreCase);
            return candidates.Length > 0 ? candidates[candidates.Length - 1] : null;
        }
    }
}
