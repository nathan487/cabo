using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Creates CardView.prefab for the network game table.</summary>
public static class CardViewBuilder
{
    [MenuItem("Tools/Build CardView Prefab")]
    public static void Build()
    {
        string prefabPath = "Assets/Prefabs/Game/CardView.prefab";
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Game");

        var go = new GameObject("CardView", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(65, 95);

        // Background Image
        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(0.12f, 0.18f, 0.28f);

        // CardBack Image
        var cbGo = new GameObject("CardBack", typeof(RectTransform), typeof(Image));
        cbGo.transform.SetParent(go.transform, false);
        var cbRt = cbGo.GetComponent<RectTransform>();
        cbRt.anchorMin = Vector2.zero; cbRt.anchorMax = Vector2.one;
        cbRt.offsetMin = new Vector2(5, 5); cbRt.offsetMax = new Vector2(-5, -5);
        cbGo.GetComponent<Image>().color = new Color(0.15f, 0.22f, 0.35f);

        // Value Text (TMP)
        var vtGo = new GameObject("ValueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        vtGo.transform.SetParent(go.transform, false);
        var vtRt = vtGo.GetComponent<RectTransform>();
        vtRt.anchorMin = Vector2.zero; vtRt.anchorMax = Vector2.one;
        vtRt.offsetMin = Vector2.zero; vtRt.offsetMax = Vector2.zero;
        var tmp = vtGo.GetComponent<TextMeshProUGUI>();
        tmp.text = "?";
        tmp.fontSize = 34;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;

        // CardView script
        var cv = go.AddComponent<Cabo.Client.Game.CardView>();

        // Wire SerializeFields via reflection
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(Cabo.Client.Game.CardView).GetField("background", flags)?.SetValue(cv, bgImg);
        typeof(Cabo.Client.Game.CardView).GetField("valueText", flags)?.SetValue(cv, tmp);
        typeof(Cabo.Client.Game.CardView).GetField("cardBack", flags)?.SetValue(cv, cbGo.GetComponent<Image>());
        typeof(Cabo.Client.Game.CardView).GetField("clickButton", flags)?.SetValue(cv, btn);

        // Save
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        Debug.Log($"[CardViewBuilder] Prefab saved: {prefabPath}");
    }
}
