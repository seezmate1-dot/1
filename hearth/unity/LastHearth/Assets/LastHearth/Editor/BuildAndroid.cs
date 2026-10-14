using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastHearth.EditorTools
{
    /// <summary>
    /// One button to a phone build: menu «Последний очаг → Собрать APK для телефона», or in batch mode
    /// <c>-executeMethod LastHearth.EditorTools.BuildAndroid.BuildFromCommandLine</c> (GitHub Actions).
    /// Sets every player setting the game needs, so a fresh checkout builds without clicking through
    /// Project Settings: name, icon, landscape, IL2CPP for ARM64 and ARMv7, the test signing key.
    /// </summary>
    public static class BuildAndroid
    {
        const string ScenePath = "Assets/LastHearth/Main.unity";
        const string IconPath = "Assets/LastHearth/Icon.png";
        const string DefaultOutput = "Builds/Android/LastHearth.apk";

        // A key for test builds only, kept in the repository so every build installs over the previous one
        // and the save stays. A Google Play release needs its own secret key (hearth/INSTALL.md).
        const string Keystore = "Keystore/lasthearth-test.keystore";
        const string KeystorePass = "lasthearth";
        const string KeyAlias = "lasthearth";

        [MenuItem("Последний очаг/Собрать APK для телефона")]
        public static void BuildFromMenu()
        {
            string path = Build(DefaultOutput);
            if (path != null)
                EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Последний очаг/Только настроить проект для Android")]
        public static void ConfigureFromMenu() => Configure();

        public static void BuildFromCommandLine()
        {
            string output = Argument("-customBuildPath") ?? DefaultOutput;
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "LastHearth.apk");
            int code = int.TryParse(Argument("-androidVersionCode"), out int c) ? c : 0;
            string path = Build(output, code);
            EditorApplication.Exit(path == null ? 1 : 0);
        }

        public static string Build(string output, int versionCode = 0)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                Debug.LogWarning("Не удалось переключиться на Android: установите модуль Android Build Support в Unity Hub");
            Configure(versionCode);
            string dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Сборка APK не удалась: " + summary.result + ", ошибок " + summary.totalErrors);
                return null;
            }
            Debug.Log(string.Format("APK готов: {0} ({1:0.0} МБ)", output, summary.totalSize / 1048576.0));
            return output;
        }

        public static void Configure(int versionCode = 0)
        {
            EnsureScene();

            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "LastHearth";
            PlayerSettings.productName = "Последний очаг";
            PlayerSettings.SetApplicationIdentifier(android, "com.lasthearth.game");
            if (versionCode > 0)
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.bundleVersion = "0.1." + versionCode;
            }

            // landscape only: the fortress is a wide cutaway, like Hustle Castle
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = false;

            // 64-bit is required by modern phones (some run only 64-bit code), ARMv7 keeps old ones working
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25; // Android 7.1, the oldest Unity 6.3 supports
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            EditorUserBuildSettings.buildAppBundle = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            if (File.Exists(Keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(Keystore);
                PlayerSettings.Android.keystorePass = KeystorePass;
                PlayerSettings.Android.keyaliasName = KeyAlias;
                PlayerSettings.Android.keyaliasPass = KeystorePass;
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>The game needs one empty scene: GameRunner starts itself after any scene loads.</summary>
        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }
    }
}
