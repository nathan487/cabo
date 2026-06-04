using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>Creates a Chinese-capable TMP Font Asset from OS fonts.</summary>
public static class ChineseTMPFontBuilder
{
    [MenuItem("Tools/Build Chinese TMP Font")]
    public static void Build()
    {
        string path = "Assets/Fonts/ChineseFont SDF.asset";
        System.IO.Directory.CreateDirectory("Assets/Fonts");

        // Try to load a Chinese system font
        Font osFont = null;
        string[] candidates = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "SimSun" };
        foreach (var name in candidates)
        {
            try { osFont = Font.CreateDynamicFontFromOSFont(name, 36); break; }
            catch { }
        }

        if (osFont == null)
        {
            Debug.LogError("[ChineseTMPFont] No Chinese font found on system!");
            return;
        }

        // Create TMP Font Asset
        var tmpFont = TMP_FontAsset.CreateFontAsset(osFont, 36, 9,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic);

        if (tmpFont == null)
        {
            Debug.LogError("[ChineseTMPFont] Failed to create TMP FontAsset!");
            return;
        }

        AssetDatabase.CreateAsset(tmpFont, path);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ChineseTMPFont] Created: {path}");
    }
}
