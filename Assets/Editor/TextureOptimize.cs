using UnityEditor;
using UnityEngine;

// 微信小游戏贴图优化：未压缩 RGBA32 是 data-package 的体积大头。
// 一张 1280×768 的照片未压缩 = 3.93MB（源 jpg 仅 0.28MB）。
// 处理：默认平台覆盖改为压缩格式 + maxTextureSize 1024（棋盘显示尺寸下肉眼无差）。
// 注意：只动 Default 平台覆盖——未显式覆盖的平台（含 WeixinMiniGame）会回落到它。
public static class TextureOptimize
{
    public static void Apply()
    {
        string[] paths = { "Assets/Resources/photo.jpg", "Assets/Textures/photo.jpg" };
        foreach (var path in paths)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                Debug.Log("[TextureOptimize] 不存在，跳过 " + path);
                continue;
            }

            var def = imp.GetDefaultPlatformTextureSettings();
            def.overridden = true;
            def.maxTextureSize = 1024;
            def.textureCompression = TextureImporterCompression.Compressed;
            def.compressionQuality = 50;
            imp.SetPlatformTextureSettings(def);
            imp.SaveAndReimport();
            Debug.Log("[TextureOptimize] 已压缩 " + path);
        }
        EditorApplication.Exit(0);
    }
}
