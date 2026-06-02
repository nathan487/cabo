using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class MainMenuSceneBuilder
{
    [MenuItem("Tools/Build MainMenu Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();

        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "MainMenuCanvas" || go.name == "EventSystem")
                Object.DestroyImmediate(go);

        if (Camera.main == null)
        {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cam.tag = "MainCamera";
            cam.GetComponent<Camera>().orthographic = true;
            cam.GetComponent<Camera>().orthographicSize = 5;
            cam.transform.position = new Vector3(0, 5, -6);
        }

        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Resolve Chinese font
        var font = ResolveChineseFont();

        var canvasGO = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = canvasGO.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var s = canvasGO.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(800, 600);
        s.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.anchoredPosition = Vector2.zero;
        prt.sizeDelta = new Vector2(400, 500);
        panel.GetComponent<Image>().color = new Color(0.05f, 0.1f, 0.2f, 0.95f);
        var menuUI = panel.AddComponent<MainMenuUI>();

        var title = MakeText(panel.transform, "Txt_Title", "Glucose Cabo\n血糖卡波", 48, 380, 100, font);
        title.rectTransform.anchoredPosition = new Vector2(0, 120);

        var subtitle = MakeText(panel.transform, "Txt_Subtitle", "热座模式 · 同设备轮流操作", 20, 360, 40, font);
        subtitle.rectTransform.anchoredPosition = new Vector2(0, 50);
        subtitle.color = new Color(0.8f, 0.8f, 0.8f);

        var btn2 = MakeButton(panel.transform, "Btn_2P", "2 人游戏", new Vector2(0, -20), new Vector2(200, 50), font);
        var btn3 = MakeButton(panel.transform, "Btn_3P", "3 人游戏", new Vector2(0, -80), new Vector2(200, 50), font);
        var btn4 = MakeButton(panel.transform, "Btn_4P", "4 人游戏", new Vector2(0, -140), new Vector2(200, 50), font);

        var info = MakeText(panel.transform, "Txt_Info", "选择玩家人数，开始一局血糖卡波！", 16, 360, 40, font);
        info.rectTransform.anchoredPosition = new Vector2(0, -200);
        info.color = new Color(0.6f, 0.6f, 0.6f);

        menuUI.titleText = title;
        menuUI.btn2Players = btn2;
        menuUI.btn3Players = btn3;
        menuUI.btn4Players = btn4;
        menuUI.infoText = info;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[MainMenuSceneBuilder] MainMenu scene built (Chinese).");
    }

    static Font ResolveChineseFont()
    {
        string[] names = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "SimSun", "KaiTi", "FangSong" };
        foreach (var n in names)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(n, 16);
                if (f != null) { Debug.Log($"[MainMenuBuilder] Font: {n}"); return f; }
            }
            catch { }
        }
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch { return null; }
    }

    static Text MakeText(Transform parent, string name, string text, int fontSize, float w, float h, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.text = text; txt.fontSize = fontSize; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white;
        if (font != null) txt.font = font;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(w, h);
        return txt;
    }

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(0.2f, 0.3f, 0.5f);
        var lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lbl.transform.SetParent(go.transform, false);
        var lt = lbl.GetComponent<Text>();
        lt.text = label; lt.fontSize = 20; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
        if (font != null) lt.font = font;
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    [MenuItem("Tools/Clear MainMenu Scene")]
    public static void Clear()
    {
        var canvas = GameObject.Find("MainMenuCanvas");
        if (canvas != null) Object.DestroyImmediate(canvas);
        var es = GameObject.Find("EventSystem");
        if (es != null) Object.DestroyImmediate(es);
        Debug.Log("[MainMenuSceneBuilder] Cleared.");
    }
}
