using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

// APK 构建脚本 - 由 Tuanjie 命令行调用
public static class AndroidBuild
{
    public static void Build()
    {
        // 配置包名
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.michaelqiu.puzzlegame");
        PlayerSettings.productName = "PuzzleGame";
        PlayerSettings.bundleVersion = "1.2.0";
        PlayerSettings.Android.bundleVersionCode = 3;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;

        // 竖屏
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        // IL2CPP or Mono - Mono更快构建（无系统NDK时用Mono+ARMv7）
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.Mono2x);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7;

        // 场景
        string[] scenes = { "Assets/Scenes/game.unity" };
        if (!File.Exists(scenes[0]))
        {
            // 找任意场景
            string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
            if (guids.Length > 0)
            {
                scenes = new string[] { AssetDatabase.GUIDToAssetPath(guids[0]) };
                Debug.Log("Using scene: " + scenes[0]);
            }
        }

        // 输出
        string outputPath = "Builds/PuzzleGame.apk";
        Directory.CreateDirectory("Builds");

        // 构建
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("✅ BUILD SUCCESS: " + outputPath);
            Debug.Log("Size: " + report.summary.totalSize / (1024*1024) + " MB");
        }
        else
        {
            Debug.LogError("❌ BUILD FAILED: " + report.summary.result);
            EditorApplication.Exit(1);
        }
        EditorApplication.Exit(0);
    }
}
