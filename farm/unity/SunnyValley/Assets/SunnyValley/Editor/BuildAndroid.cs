using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SunnyValley.EditorTools
{
    /// <summary>
    /// One button to a phone build: menu «Солнечная долина → Собрать APK для телефона», or in batch mode
    /// <c>-executeMethod SunnyValley.EditorTools.BuildAndroid.BuildFromCommandLine</c> (GitHub Actions, farm-android.yml).
    /// Sets every player setting the game needs, so a fresh checkout builds without clicking through Project Settings:
    /// name, icon, portrait only, drawing under the cut-out (the interface keeps to Screen.safeArea itself),
    /// IL2CPP for ARM64 and ARMv7, the test signing key. The scene is made in code: the game starts itself (GameApp).
    /// </summary>
    public static class BuildAndroid
    {
        const string ScenePath = "Assets/SunnyValley/Main.unity";
        const string IconPath = "Assets/SunnyValley/Icon.png";
        const string DefaultOutput = "Builds/Android/SunnyValley.apk";

        // A key for test builds only, kept in the repository so every build installs over the previous one and the
        // progress stays. A Google Play release needs its own secret key, never committed.
        const string Keystore = "Keystore/sunnyvalley-test.keystore";
        const string KeystorePass = "sunnyvalley";
        const string KeyAlias = "sunnyvalley";

        [MenuItem("Солнечная долина/Собрать APK для телефона")]
        public static void BuildFromMenu()
        {
            string path = Build(DefaultOutput);
            if (path != null)
                EditorUtility.RevealInFinder(path);
        }

        [MenuItem("Солнечная долина/Только настроить проект для Android")]
        public static void ConfigureFromMenu() => Configure();

        public static void BuildFromCommandLine()
        {
            string output = Argument("-customBuildPath") ?? DefaultOutput;
            if (!output.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                output = Path.Combine(output, "SunnyValley.apk");
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
            PlayerSettings.companyName = "SunnyValley";
            PlayerSettings.productName = "Солнечная долина";
            PlayerSettings.SetApplicationIdentifier(android, "com.sunnyvalley.game");
            if (versionCode > 0)
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.bundleVersion = "0.1." + versionCode;
            }

            // portrait only (GDD: a vertical screen)
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;

            // full screen, drawn under the camera cut-out: backgrounds fill the whole glass, while every button
            // keeps to Screen.safeArea (the layout model, checked by farm/ui-tests)
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

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

        /// <summary>The game needs one empty scene: GameApp starts itself after any scene loads.</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var cam = Camera.main;
                if (cam != null)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color32(0x9E, 0xD9, 0xF6, 0xFF);
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        public static string Argument(string name)
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
