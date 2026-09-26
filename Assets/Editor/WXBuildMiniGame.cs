using UnityEditor;
using UnityEngine;

namespace WeChatWASM
{
    // 供 batchmode 调用的 0 参包装：走插件官方「微信小游戏 / 转换小游戏」流程（构建 WebGL → 转换到 minigame/）。
    // 为什么要它：WXConvertCore.DoExport 带可选参数（bool buildWebGL = true），Unity 的 -executeMethod
    // 只认 0 参或单 string 参的方法，直接指过去会找不到方法。
    // 只做 Unity 侧 BuildPipeline 的脚本（Assets/Editor/WeixinMiniGameBuild.cs）不会刷新 minigame/，
    // 必须经插件这一步才会重新生成 wasmcode / data-package / symbols 并写入 minigame 目录。
    public static class WXBuildMiniGame
    {
        public static void Convert()
        {
            var err = WXConvertCore.DoExport(true);
            Debug.Log("[WXBuildMiniGame] DoExport -> " + err);
            EditorApplication.Exit(err == WXConvertCore.WXExportError.SUCCEED ? 0 : 1);
        }
    }
}
