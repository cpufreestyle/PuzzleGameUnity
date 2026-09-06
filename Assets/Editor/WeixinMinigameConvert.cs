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

        // Appid: touristappid 为微信官方测试号，正式发布前替换为真实 AppID
        config.ProjectConf.Appid = "touristappid";
        config.ProjectConf.projectName = "PuzzleGame";
        config.ProjectConf.relativeDST = "Builds/WeChatMiniGame";
        config.ProjectConf.DST = Path.GetFullPath("Builds/WeChatMiniGame");
        config.ProjectConf.CDN = "";
        config.ProjectConf.StreamCDN = "";

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
