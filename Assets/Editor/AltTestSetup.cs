using UnityEngine;
using UnityEditor;
using AltTester.AltTesterUnitySDK.Editor;
using AltTester.AltTesterSDK.Driver;

// AltTester 接线脚本 - 由 Tuanjie 命令行调用
// Setup: 添加 ALTTESTER 宏 + 把 AltTesterPrefab 塞进主场景（幂等）
// EnterPlay: batchmode 下进入 Play 模式，供 AltDriver(Python) 从 127.0.0.1:13000 连接
public static class AltTestSetup
{
    const string ScenePath = "Assets/Scenes/game.unity";

    public static void Setup()
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        AltBuilder.AddAltTesterInScriptingDefineSymbolsGroup(group);
        Debug.Log("[AltTestSetup] defines for " + group + " = " +
                  PlayerSettings.GetScriptingDefineSymbolsForGroup(group));

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        if (!scene.isLoaded)
        {
            Debug.LogError("[AltTestSetup] failed to open " + ScenePath);
            EditorApplication.Exit(2);
        }

        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "AltTesterPrefab") { exists = true; break; }
        }

        if (exists)
        {
            Debug.Log("[AltTestSetup] AltTesterPrefab already in scene, skip insert");
        }
        else
        {
            AltBuilder.InsertAltTesterInScene(ScenePath,
                new AltInstrumentationSettings { hideGreenPopup = true });
            Debug.Log("[AltTestSetup] AltTesterPrefab inserted into " + ScenePath);
        }

        Debug.Log("ALTTESTER-SETUP-OK");
    }

    public static void EnterPlay()
    {
        Debug.Log("[AltTestSetup] entering Play mode for AltDriver...");
        EditorApplication.isPlaying = true;
    }
}
