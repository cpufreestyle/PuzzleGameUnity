using UnityEngine;
using UnityEditor;
using WeChatWASM;
using System.IO;

// 微信小游戏转换脚本 - 由 Tuanjie 命令行调用（依赖 com.qq.weixin.minigame 转换SDK）
public static class WeixinMinigameConvert
{
    public static void Convert()
    {
        var config = UnityUtil.GetEditorConf();

        // 正式小游戏 AppID
        config.ProjectConf.Appid = "wx27892251b37342a7";
        config.ProjectConf.projectName = "PuzzleGame";
        config.ProjectConf.relativeDST = "Builds/WeChatMiniGame";
        config.ProjectConf.DST = Path.GetFullPath("Builds/WeChatMiniGame");
        config.ProjectConf.CDN = "";
        config.ProjectConf.StreamCDN = "";
        // 1 = 资源走小游戏分包（data-package）本地加载；0 = 走 CDN（无 CDN 会卡"正在加载资源"）
        config.ProjectConf.assetLoadType = 1;

        var err = WXConvertCore.DoExport(true);
        if (err == WXConvertCore.WXExportError.SUCCEED)
        {
            Debug.Log("✅ WX CONVERT SUCCESS: " + config.ProjectConf.DST + "/minigame");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("❌ WX CONVERT FAILED: " + err);
            EditorApplication.Exit(1);
        }
    }
}
