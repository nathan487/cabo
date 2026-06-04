using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PlayerAreaBuilder
{
    [MenuItem("Tools/Build PlayerArea Prefab")]
    public static void Build()
    {
        string path = "Assets/Prefabs/Game/PlayerArea.prefab";
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Game");

        var go = new GameObject("PlayerArea", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(260, 100);

        // Background
        var bg = NewChild(go.transform, "Background", rt.sizeDelta, Vector2.zero);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.1f, 0.18f, 0.85f);

        // Highlight border
        var hl = NewChild(go.transform, "Highlight", rt.sizeDelta + new Vector2(8, 8), Vector2.zero);
        var hlImg = hl.AddComponent<Image>();
        hlImg.color = new Color(0.25f, 0.25f, 0.35f);

        var font = GetFont();

        // Name (legacy Text for Chinese support)
        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(go.transform, false);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
        nameRt.sizeDelta = new Vector2(250, 22);
        nameRt.anchoredPosition = new Vector2(0, -14);
        var nameTxt = nameGo.GetComponent<Text>();
        nameTxt.text = "Player"; nameTxt.fontSize = 14;
        nameTxt.alignment = TextAnchor.MiddleCenter; nameTxt.color = Color.white;
        nameTxt.font = font; nameTxt.raycastTarget = false;

        // Score (legacy Text)
        var scoreGo = new GameObject("Score", typeof(RectTransform), typeof(Text));
        scoreGo.transform.SetParent(go.transform, false);
        var scoreRt = scoreGo.GetComponent<RectTransform>();
        scoreRt.anchorMin = scoreRt.anchorMax = new Vector2(0.5f, 1f);
        scoreRt.sizeDelta = new Vector2(250, 18);
        scoreRt.anchoredPosition = new Vector2(0, -32);
        var scoreTxt = scoreGo.GetComponent<Text>();
        scoreTxt.text = "0 分"; scoreTxt.fontSize = 12;
        scoreTxt.alignment = TextAnchor.MiddleCenter; scoreTxt.color = new Color(0.7f, 0.8f, 0.9f);
        scoreTxt.font = font; scoreTxt.raycastTarget = false;

        // Cards container
        var cardsGo = new GameObject("CardsContainer", typeof(RectTransform));
        cardsGo.transform.SetParent(go.transform, false);
        var cardsRt = cardsGo.GetComponent<RectTransform>();
        cardsRt.anchorMin = cardsRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRt.sizeDelta = new Vector2(240, 55);
        cardsRt.anchoredPosition = new Vector2(0, -5);

        // PlayerAreaView script
        var area = go.AddComponent<Cabo.Client.Game.PlayerAreaView>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(Cabo.Client.Game.PlayerAreaView).GetField("nameText", flags)?.SetValue(area, nameTxt);
        typeof(Cabo.Client.Game.PlayerAreaView).GetField("scoreText", flags)?.SetValue(area, scoreTxt);
        typeof(Cabo.Client.Game.PlayerAreaView).GetField("highlightBorder", flags)?.SetValue(area, hlImg);
        typeof(Cabo.Client.Game.PlayerAreaView).GetField("cardsContainer", flags)?.SetValue(area, cardsRt);

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[PlayerAreaBuilder] Prefab saved: {path}");
    }

    private static Font GetFont()
    {
        string[] names = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "SimSun" };
        foreach (var n in names)
            try { var f = Font.CreateDynamicFontFromOSFont(n, 16); if (f != null) return f; } catch { }
        return Font.CreateDynamicFontFromOSFont("Arial", 16);
    }

    private static GameObject NewChild(Transform parent, string name, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return go;
    }
}
