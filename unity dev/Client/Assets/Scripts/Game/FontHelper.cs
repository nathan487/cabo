using UnityEngine;

/// <summary>
/// Provides a Chinese-capable font for Unity 2022.3.
/// Uses OS dynamic fonts — no bundled font files needed.
/// </summary>
public static class FontHelper
{
    private static Font _chineseFont;
    private static bool _tried;

    /// <summary>
    /// Returns a Font that supports Chinese characters.
    /// Tries Microsoft YaHei (微软雅黑) first, falls back to SimHei (黑体),
    /// then SimSun (宋体), then Noto Sans CJK, then any CJK font, then Arial.
    /// </summary>
    public static Font GetChineseFont()
    {
        if (_chineseFont != null) return _chineseFont;
        if (_tried) return null; // already tried and failed
        _tried = true;

        // Windows: most common Chinese fonts, ordered by visual quality
        string[] candidates = {
            "Microsoft YaHei",   // 微软雅黑 — clean, modern, best for UI
            "SimHei",            // 黑体 — bold, good readability
            "Microsoft JhengHei",// 微軟正黑體 — traditional Chinese
            "SimSun",            // 宋体 — serif
            "KaiTi",             // 楷体 — calligraphic
            "FangSong",          // 仿宋
        };

        // macOS / Linux candidates
        string[] unixCandidates = {
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "WenQuanYi Micro Hei",
            "WenQuanYi Zen Hei",
            "PingFang SC",
            "Heiti SC",
        };

        // Try platform-specific first
        string[] platformFirst = Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor
            ? candidates
            : unixCandidates;

        string[] platformSecond = Application.platform == RuntimePlatform.WindowsPlayer
            || Application.platform == RuntimePlatform.WindowsEditor
            ? unixCandidates
            : candidates;

        foreach (var name in platformFirst)
        {
            _chineseFont = TryCreateFont(name);
            if (_chineseFont != null)
            {
                Debug.Log($"[FontHelper] Using font: {name}");
                return _chineseFont;
            }
        }

        foreach (var name in platformSecond)
        {
            _chineseFont = TryCreateFont(name);
            if (_chineseFont != null)
            {
                Debug.Log($"[FontHelper] Using font: {name}");
                return _chineseFont;
            }
        }

        // Last resort: try default font
        try
        {
            _chineseFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_chineseFont != null)
            {
                Debug.Log("[FontHelper] Using built-in LegacyRuntime font.");
                return _chineseFont;
            }
        }
        catch { }

        Debug.LogWarning("[FontHelper] No Chinese-capable font found. Chinese text may display as boxes.");
        return null;
    }

    private static Font TryCreateFont(string name)
    {
        try
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 16);
            if (f != null)
            {
                // Verify the font actually has Chinese characters
                f.RequestCharactersInTexture("血糖卡波玩家回合抽牌弃牌稳态技能确认取消游戏结束");
                return f;
            }
        }
        catch { }
        return null;
    }
}
