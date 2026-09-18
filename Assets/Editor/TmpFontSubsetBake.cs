using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// TMP 中文字体子集烘焙工具（常驻）。
// 用法：Tuanjie 菜单 Tools/TMP/重烘中文子集，或批处理 -executeMethod TmpFontSubsetBake.Execute
// 自动扫描 Assets/Scripts/*.cs 字符串字面量里的 CJK/全角字符并烘进
// NotoSansSC-Regular SDF（Static 子集，源 OTF 不入包）。UI 新增文案后重跑即可。
// 原理：TryAddCharacters 只支持 Dynamic → 先 Dynamic 烘焙 → 反射清 m_SourceFontFile
//       （否则 8.3MB 源字体会打进包）→ 切回 Static，字形留在图集。
public static class TmpFontSubsetBake
{
    const string FontDir = "Assets/Codely/Fonts";
    const string FontSourcePath = FontDir + "/NotoSansSC-Regular.otf";
    const string FontAssetPath = FontDir + "/NotoSansSC-Regular SDF.asset";
    // 手动兜底字符（UI 动态拼接可能出现、但扫描不到的：全角标点等）
    const string ExtraChars = "：？，。！";

    [MenuItem("Tools/TMP/重烘中文子集")]
    public static void MenuRun()
    {
        Debug.Log("TMP_BAKE_REPORT:\n" + Run());
    }

    public static void Execute()
    {
        try
        {
            Debug.Log("TMP_BAKE_REPORT:\n" + Run());
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("TMP_BAKE_FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    static string Run()
    {
        var settings = TMP_Settings.instance;
        if (settings == null) throw new InvalidOperationException("TMP_SETTINGS_MISSING");

        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (sourceFont == null) throw new InvalidOperationException("TMP_SOURCE_FONT_MISSING " + FontSourcePath);

        var charset = new StringBuilder(ExtraChars);
        foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                foreach (var c in m.Groups[1].Value)
                    if (((c >= 0x3000 && c <= 0x9FFF) || (c >= 0xFF00 && c <= 0xFFEF))
                        && charset.ToString().IndexOf(c) < 0)
                        charset.Append(c);
        }
        // 排除代理对（emoji 等 BMP 外字符，子集不支持且字体无字形）
        for (int i = charset.Length - 1; i >= 0; i--)
            if (char.IsSurrogate(charset[i]))
                charset.Remove(i, 1);

        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null) throw new InvalidOperationException("FONT_ASSET_MISSING " + FontAssetPath);

        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        var missing = new StringBuilder();
        if (!fontAsset.TryAddCharacters(charset.ToString()))
            Debug.LogWarning("TMP_BAKE partial: some chars failed, see missing list");
        foreach (var c in charset.ToString())
            if (!fontAsset.HasCharacter(c))
                missing.Append(c);

        typeof(TMP_FontAsset)
            .GetField("m_SourceFontFile",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(fontAsset, null);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        var fallbacks = TMP_Settings.fallbackFontAssets;
        if (fallbacks != null && !fallbacks.Contains(fontAsset))
            fallbacks.Add(fontAsset);

        EditorUtility.SetDirty(fontAsset);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return (missing.Length == 0
                ? $"BAKED all={charset.Length}"
                : $"BAKED_MISSING:{missing}")
            + $" totalChars={charset.Length} asset={FontAssetPath}";
    }
}
