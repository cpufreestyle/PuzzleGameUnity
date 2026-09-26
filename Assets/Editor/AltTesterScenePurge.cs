using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// 一次性清理：从主场景移除 AltTesterPrefab 实例。
// 背景：AltTester SDK 已改为 defineConstraints:["ALTTESTER"] 守卫，正式构建不再包含其程序集；
// 场景里残留的 prefab 实例会变成 missing script，且它自带 AltCanvas/AltDialog（整套 UI 弹窗）会留下视觉残骸。
// 不引用 AltTester 命名空间，故在未定义 ALTTESTER 时也能编译。
// 需要恢复自动化时：给目标平台组加回 ALTTESTER 宏，再跑 AltTestSetup.Setup() 即可重新插入。
public static class AltTesterScenePurge
{
    const string ScenePath = "Assets/Scenes/game.unity";

    public static void Purge()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!scene.isLoaded)
        {
            Debug.LogError("[AltTesterScenePurge] 无法打开场景 " + ScenePath);
            EditorApplication.Exit(2);
        }

        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == "AltTesterPrefab")
            {
                Debug.Log("[AltTesterScenePurge] 移除 " + root.name);
                Object.DestroyImmediate(root);
                removed++;
            }
        }

        if (removed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[AltTesterScenePurge] 已保存场景，removed=" + removed);
        }
        else
        {
            Debug.Log("[AltTesterScenePurge] 场景中无 AltTesterPrefab，跳过");
        }

        EditorApplication.Exit(0);
    }
}
