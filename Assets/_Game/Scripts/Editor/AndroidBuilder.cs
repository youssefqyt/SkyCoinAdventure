using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SkyCoin.EditorTools
{
    /// <summary>
    /// Command line Android build. No menu item: built from terminal.
    ///
    /// TWO-STEP USAGE (needed because Unity caches Android tool paths at startup):
    ///
    ///   Step 1 (run once): Set EditorPrefs and exit.
    ///     Unity.exe -batchmode -quit -projectPath "..." ^
    ///       -executeMethod SkyCoin.EditorTools.AndroidBuilder.SetExternalTools
    ///
    ///   Step 2 (actual build): Fresh Unity session reads prefs at startup.
    ///     Unity.exe -batchmode -quit -projectPath "..." ^
    ///       -executeMethod SkyCoin.EditorTools.AndroidBuilder.BuildApk ^
    ///       -logFile "C:\...\Builds\Android\build.log"
    ///
    /// Output: Builds/Android/SkyCoinAdventure.apk
    /// Exit code 0 = success, 1 = failure.
    /// </summary>
    public static class AndroidBuilder
    {
        private const string ApkFileName = "SkyCoinAdventure.apk";

        private static readonly string[] GameScenes =
        {
            "Assets/_Game/Scenes/MainMenu.unity",
            "Assets/_Game/Scenes/Level_01.unity",
            "Assets/_Game/Scenes/Level_02.unity"
        };

        /// <summary>
        /// Run FIRST: persists Android SDK/NDK/JDK paths to EditorPrefs, then exits.
        /// Run this in its own Unity session so the Android extension reads them at
        /// startup on the next session.
        /// </summary>
        public static void SetExternalTools()
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
                    string[] versions = Directory.GetDirectories(ndkRoot);
                    Array.Sort(versions, StringComparer.OrdinalIgnoreCase);
                    if (versions.Length > 0) ndk = versions[versions.Length - 1];
                }
            }

            // JDK for Unity 6 = Java 17 only. IMPORTANT: Unity 6000.x reads the
            // EditorPrefs key "Jdk17Path" (versioned key), NOT the old "JdkPath".
            // The JDK's "java -version" output must be a short version (max 4 parts,
            // e.g. "17.0.21") — long vendor versions like "17.0.21.0.3" (5 parts)
            // fail Unity's Version.Parse and give "JDK not found".
            string jdk = FirstExisting(
                GetUserLatestJdk(Path.Combine(localAppData, "Programs", "Eclipse Adoptium"), "jdk-17*"),
                GetUserLatestJdk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium"), "jdk-17*"),
                GetUserLatestJdk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft"), "jdk-17*"),
                IsFullJdk(Environment.GetEnvironmentVariable("JAVA_HOME")) ? Environment.GetEnvironmentVariable("JAVA_HOME") : null,
                GetUserLatestJdk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zulu"), "zulu-17*"));

            EditorPrefs.SetBool("AndroidUseEmbeddedSDK", true);
            EditorPrefs.SetBool("AndroidUseEmbeddedNDK", true);

            if (!string.IsNullOrEmpty(sdk))
            {
                sdk = Path.GetFullPath(sdk);
                EditorPrefs.SetString("AndroidSdkRoot", sdk);
                Environment.SetEnvironmentVariable("ANDROID_HOME", sdk);
                Environment.SetEnvironmentVariable("ANDROID_SDK_ROOT", sdk);
                Debug.Log($"[AndroidBuilder] SDK: {sdk}");
            }
            if (!string.IsNullOrEmpty(ndk))
            {
                ndk = Path.GetFullPath(ndk);
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                Environment.SetEnvironmentVariable("ANDROID_NDK_ROOT", ndk);
                Debug.Log($"[AndroidBuilder] NDK: {ndk}");
            }
            if (!string.IsNullOrEmpty(jdk))
            {
                jdk = Path.GetFullPath(jdk);
                // Use the external JDK: correct pref key is "Jdk17Path" (Unity 6000.x).
                EditorPrefs.SetBool("JdkUseEmbedded", false);
                EditorPrefs.SetString("Jdk17Path", jdk);
                Environment.SetEnvironmentVariable("JAVA_HOME", jdk);
                Debug.Log($"[AndroidBuilder] JDK: {jdk}");
            }
            else
            {
                Debug.LogWarning("[AndroidBuilder] No JDK 17 found. Unity will use its own detection (JAVA_HOME).");
            }

            Debug.Log("[AndroidBuilder] SetExternalTools complete. EditorPrefs saved. " +
                      "Now run BuildApk in a new Unity session.");
            EditorApplication.Exit(0);
        }

        public static void BuildApk()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[AndroidBuilder] Android Build Support NOT installed.");
                EditorApplication.Exit(1);
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Builds", "Android");
            Directory.CreateDirectory(outputDirectory);
            string apkPath = Path.Combine(outputDirectory, ApkFileName);

            ConfigurePlayer();
            string[] scenes = ConfigureBuildScenes(projectRoot);
            if (scenes == null)
            {
                EditorApplication.Exit(1);
                return;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[AndroidBuilder] Could not switch to Android target.");
                    EditorApplication.Exit(1);
                    return;
                }
            }

            EditorUserBuildSettings.buildAppBundle = false;

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
                double mb = report.summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[AndroidBuilder] SUCCESS: {apkPath} ({mb:0.0} MB)");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AndroidBuilder] FAILED: {report.summary.result} " +
                               $"({report.summary.totalErrors} error(s)).");
                EditorApplication.Exit(1);
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

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        private static string[] ConfigureBuildScenes(string projectRoot)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            List<string> paths = new List<string>();
            foreach (string scene in GameScenes)
            {
                if (!File.Exists(Path.Combine(projectRoot, scene)))
                {
                    Debug.LogWarning($"[AndroidBuilder] Scene missing: {scene}");
                    continue;
                }
                scenes.Add(new EditorBuildSettingsScene(scene, true));
                paths.Add(scene);
            }
            if (scenes.Count == 0)
            {
                Debug.LogError("[AndroidBuilder] No game scenes found.");
                return null;
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[AndroidBuilder] Scenes: {string.Join(", ", paths)}");
            return paths.ToArray();
        }

        /// <summary>
        /// Diagnostic: run Unity's own JDK detection code (reflection, internal classes)
        /// and log every step + error message so we know exactly why validation fails.
        /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod SkyCoin.EditorTools.AndroidBuilder.DiagnoseAndroidJdk -logFile <log>
        /// </summary>
        public static void DiagnoseAndroidJdk()
        {
            System.Reflection.Assembly asm;
            try
            {
                string extDll =
                    @"C:\Program Files\Unity\Hub\Editor\6000.4.4f1\Editor\Data\PlaybackEngines\AndroidPlayer\UnityEditor.Android.Extensions.dll";
                Log($"extDll='{extDll}' exists={File.Exists(extDll)}");
                asm = File.Exists(extDll)
                    ? System.Reflection.Assembly.LoadFrom(extDll)
                    : typeof(BuildPipeline).Assembly;
            }
            catch (Exception e)
            {
                Log($"Could not load Android Extensions dll: {e.Message}");
                Fail();
                return;
            }
            Log("=== AndroidBuilder JDK diagnose ===");

            Log($"Loaded asm: {asm.FullName} from {asm.Location}");
            try
            {
                Type[] all = asm.GetTypes();
                Log($"Type count: {all.Length}");
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Log($"ReflectionTypeLoadException, loaded={rtle.Types.Count(t => t != null)}, loaderErrors={rtle.LoaderExceptions.Length}");
                foreach (Exception le in rtle.LoaderExceptions.Take(5))
                    Log($"  loaderError: {le.Message}");
            }

            Type rootType = asm.GetType("UnityEditor.Android.AndroidJavaRoot");
            Type toolsType = asm.GetType("UnityEditor.Android.AndroidJavaTools");
            Log($"rootType={(rootType == null ? "NULL" : rootType.FullName)}, toolsType={(toolsType == null ? "NULL" : toolsType.FullName)}");
            if (rootType == null || toolsType == null)
            {
                Fail();
                return;
            }

            const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            // 1. EditorPrefs as Unity reads them
            Log($"Prefs: JdkUseEmbedded={EditorPrefs.GetBool("JdkUseEmbedded")} " +
                $"Jdk17Path='{EditorPrefs.GetString("Jdk17Path")}' " +
                $"JdkPath='{EditorPrefs.GetString("JdkPath")}'");

            // 2. AndroidJavaRoot state
            object root = rootType.GetMethod("GetInstance", AnyStatic, null, Type.EmptyTypes, null).Invoke(null, null);
            string embeddedKey = rootType.GetProperty("EmbeddedPreferenceKey", AnyStatic).GetValue(root) as string;
            string dirKey = rootType.GetProperty("DirectoryPreferenceKey", AnyStatic).GetValue(root) as string;
            string envVar = rootType.GetProperty("EnvironmentVarName", AnyStatic).GetValue(root) as string;
            string embeddedSub = rootType.GetProperty("EmbeddedDirSubPath", AnyStatic).GetValue(root) as string;
            Version req = (Version)rootType.GetProperty("RequiredVersion", AnyStatic).GetValue(root);
            Log($"EmbeddedKey='{embeddedKey}' DirKey='{dirKey}' EnvVar='{envVar}' EmbeddedSubPath='{embeddedSub}' RequiredVersion={req}");

            bool useEmbedded = (bool)rootType.GetProperty("UseEmbedded", AnyStatic).GetValue(root);
            Log($"UseEmbedded(resolved)={useEmbedded}");
            rootType.GetMethod("SyncPreferences", AnyStatic, null, Type.EmptyTypes, null).Invoke(root, null);

            string rootDir = rootType.GetMethod("GetRootDirectory", AnyStatic, null, Type.EmptyTypes, null).Invoke(root, null) as string;
            Log($"GetRootDirectory -> '{rootDir}'");

            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            Log($"JAVA_HOME env = '{javaHome}'");

            // 3. Validate each candidate directory with Unity's own Validate()
            string[] candidates = { rootDir, javaHome, @"C:\Program Files\Unity\Hub\Editor\6000.4.4f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK", @"C:\Program Files\Zulu\zulu-17" };
            MethodInfo validate = rootType.GetMethod("Validate", AnyStatic, null, new[] { typeof(string) }, null);

            foreach (string dir in candidates.Distinct())
            {
                Log($"--- Validate('{dir}')");
                Log($"    Directory.Exists = {Directory.Exists(dir ?? "")}; bin\\java.exe = {File.Exists(Path.Combine(dir ?? "", "bin", "java.exe"))}; bin\\javac.exe = {File.Exists(Path.Combine(dir ?? "", "bin", "javac.exe"))}");
                try
                {
                    object vr = validate.Invoke(root, new object[] { dir });
                    Type vrType = vr.GetType();
                    string msg = vrType.GetProperty("ErrorMessage").GetValue(vr) as string;
                    string rdir = vrType.GetProperty("RootDirectory").GetValue(vr) as string;
                    bool success = (bool)vrType.GetProperty("Success").GetValue(vr);
                    Log($"    Success={success} RootDirectory='{rdir}' Error='{msg}'");
                }
                catch (Exception e)
                {
                    Log($"    Validate threw: {e.InnerException ?? e}");
                }
            }

            // 4. AndroidJavaTools.GetInstance() — the decisive call
            try
            {
                object tools = toolsType.GetMethod("GetInstance", AnyStatic, null, Type.EmptyTypes, null).Invoke(null, null);
                Log($"AndroidJavaTools.GetInstance -> {(tools == null ? "NULL (this is the 'JDK not found' cause)" : tools.ToString())}");
            }
            catch (Exception e)
            {
                Log($"GetInstance threw: {e.InnerException ?? e}");
            }

            // 5. Direct probe: what does Unity's own version detection get?
            MethodInfo getVersionText = rootType.GetMethod("GetVersionText", AnyStatic, null, new[] { typeof(string) }, null);
            foreach (string dir in new[] { @"C:\Program Files\Zulu\zulu-17", @"C:\Program Files\Unity\Hub\Editor\6000.4.4f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK" })
            {
                try
                {
                    string vt = getVersionText.Invoke(root, new object[] { dir }) as string;
                    Log($"GetVersionText('{dir}') -> len={(vt ?? "").Length} text=[{vt}]");
                }
                catch (Exception e)
                {
                    Log($"GetVersionText threw: {e.InnerException ?? e}");
                }
            }

            Log("=== diagnose end ===");
            EditorApplication.Exit(0);
        }

        private static void Fail()
        {
            Debug.LogError("[AndroidBuilder] Diagnose failed (missing types).");
            EditorApplication.Exit(1);
        }

        private static void Log(string message)
        {
            Debug.Log($"[AndroidBuilder] {message}");
        }

        private static string FirstExisting(params string[] paths)
        {
            foreach (string p in paths)
                if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
            return null;
        }

        /// <summary>A usable JDK needs bin\java.exe AND bin\javac.exe.</summary>
        private static bool IsFullJdk(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return false;
            return File.Exists(Path.Combine(directory, "bin", "java.exe"))
                && File.Exists(Path.Combine(directory, "bin", "javac.exe"));
        }

        /// <summary>Finds the newest folder matching a pattern under a parent folder (e.g. Microsoft\jdk-17*).</summary>
        private static string GetUserLatestJdk(string parent, string searchPattern)
        {
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)) return null;
            try
            {
                string[] dirs = Directory.GetDirectories(parent, searchPattern);
                // newest first; return the first one that is a usable full JDK
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
                Array.Reverse(dirs);
                foreach (string dir in dirs)
                    if (IsFullJdk(dir)) return dir;
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}