using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Builds the Game scene UI with Chinese text.
/// Run via Tools > Build Game Scene.
/// </summary>
public static class GameSceneBuilder
{
    [MenuItem("Tools/Build Game Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == "GameCanvas" || go.name == "Canvas" || go.name == "EventSystem")
                Object.DestroyImmediate(go);

        var gameSetup = GameObject.Find("GameSetup");
        if (gameSetup == null) gameSetup = new GameObject("GameSetup");
        if (gameSetup.GetComponent<CaboGameManager>() == null)
            gameSetup.AddComponent<CaboGameManager>();
        if (gameSetup.GetComponent<GameSceneBootstrap>() == null)
            gameSetup.AddComponent<GameSceneBootstrap>();

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

        var font = ResolveChineseFont();

        var canvasGO = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 0.5f;

        // ── GamePanel ──
        var gamePanel = NewGO("GamePanel", canvasGO.transform, new Vector2(800, 600));
        var gameUI = gamePanel.AddComponent<GameUI>();

        // HUD Bar
        var hudBar = MakePanel(gamePanel.transform, "HudBar", new Vector2(700, 60),
            new Color(0.05f, 0.08f, 0.15f, 0.9f));
        SetPos(hudBar, 0, 250);

        var curPlayerText = MakeText(hudBar.transform, "Txt_CurrentPlayer", "玩家", 24, 300, 40, font);
        SetPos(curPlayerText.rectTransform, -200, 0);
        var roundText = MakeText(hudBar.transform, "Txt_Round", "第 1 轮", 18, 150, 30, font);
        SetPos(roundText.rectTransform, 0, 0);
        var phaseHint = MakeText(hudBar.transform, "Txt_PhaseHint", "", 16, 250, 30, font);
        SetPos(phaseHint.rectTransform, 180, 0);

        // Pile Area
        var pileArea = MakePanel(gamePanel.transform, "PileArea", new Vector2(300, 180), null);
        SetPos(pileArea, 0, 80);
        MakeButton(pileArea.transform, "Btn_DrawPile", "抽牌堆", new Vector2(-70, 0), new Vector2(100, 120),
            new Color(0.15f, 0.4f, 0.6f), font);
        var drawCount = MakeText(pileArea.transform, "Txt_DrawCount", "52", 14, 50, 20, font);
        drawCount.rectTransform.anchoredPosition = new Vector2(-70, -70);
        MakeButton(pileArea.transform, "Btn_DiscardPile", "弃牌堆", new Vector2(70, 0), new Vector2(100, 120),
            new Color(0.3f, 0.35f, 0.5f), font);
        var discardTop = MakeText(pileArea.transform, "Txt_DiscardTop", "-", 20, 50, 20, font);
        discardTop.rectTransform.anchoredPosition = new Vector2(70, -70);

        // Card Slots
        var slotsArea = MakePanel(gamePanel.transform, "CardSlots", new Vector2(500, 160), null);
        SetPos(slotsArea, 0, -80);
        var cardSlots = new Button[4];
        var cardValueTexts = new Text[4];
        var cardBackgrounds = new Image[4];
        var cardSlotRects = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 110;
            var slot = MakeButton(slotsArea.transform, "CardSlot" + i, "?",
                new Vector2(x, 0), new Vector2(90, 130), new Color(0.12f, 0.18f, 0.28f), font);
            cardSlots[i] = slot;
            cardValueTexts[i] = slot.GetComponentInChildren<Text>();
            cardBackgrounds[i] = slot.GetComponent<Image>();
            cardSlotRects[i] = slot.GetComponent<RectTransform>();
        }

        // Action Buttons
        var actionArea = MakePanel(gamePanel.transform, "ActionPanel", new Vector2(180, 280), null);
        SetPos(actionArea, 300, 0);
        var btnDraw = MakeButton(actionArea.transform, "Btn_Draw", "从牌堆抽牌",
            new Vector2(0, 100), new Vector2(160, 45), new Color(0.15f, 0.4f, 0.6f), font);
        var btnTakeDiscard = MakeButton(actionArea.transform, "Btn_TakeDiscard", "从弃牌堆拿",
            new Vector2(0, 45), new Vector2(160, 45), new Color(0.3f, 0.35f, 0.5f), font);
        var btnCallSteady = MakeButton(actionArea.transform, "Btn_CallSteady", "喊稳态！",
            new Vector2(0, -10), new Vector2(160, 45), new Color(0.5f, 0.15f, 0.2f), font);
        var btnDiscardDrawn = MakeButton(actionArea.transform, "Btn_DiscardDrawn", "弃掉此牌",
            new Vector2(0, -65), new Vector2(160, 40), new Color(0.3f, 0.3f, 0.3f), font);
        btnDiscardDrawn.gameObject.SetActive(false);
        var btnReplaceCard = MakeButton(actionArea.transform, "Btn_ReplaceCard", "替换卡牌",
            new Vector2(0, -110), new Vector2(160, 40), new Color(0.4f, 0.3f, 0.15f), font);
        btnReplaceCard.gameObject.SetActive(false);
        var btnUseSkill = MakeButton(actionArea.transform, "Btn_UseSkill", "使用技能！",
            new Vector2(0, -155), new Vector2(160, 40), new Color(0.5f, 0.3f, 0.1f), font);
        btnUseSkill.gameObject.SetActive(false);

        // Drawn Preview
        var drawnPreview = MakePanel(gamePanel.transform, "DrawnPreview", new Vector2(130, 60),
            new Color(0.2f, 0.25f, 0.35f, 0.95f));
        SetPos(drawnPreview, 250, 160);
        var drawnPreviewText = MakeText(drawnPreview.transform, "Txt_DrawnCard", "抽到：?", 16, 120, 50, font);
        drawnPreview.SetActive(false);

        // Score Panel
        var scorePanel = MakePanel(gamePanel.transform, "ScorePanel", new Vector2(200, 120),
            new Color(0.05f, 0.08f, 0.15f, 0.9f));
        SetPos(scorePanel, 330, 220);
        MakeText(scorePanel.transform, "Txt_ScoreTitle", "分数", 16, 100, 25, font)
            .rectTransform.anchoredPosition = new Vector2(0, 40);
        var scoreTexts = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            scoreTexts[i] = MakeText(scorePanel.transform, "Txt_Score" + i, "", 14, 180, 22, font);
            scoreTexts[i].rectTransform.anchoredPosition = new Vector2(0, 15 - i * 25);
        }

        // ── Overlays ──
        BuildPassScreen(canvasGO, gameUI, font);
        BuildSkillPanel(canvasGO, gameUI, font);
        BuildRoundEndPanel(canvasGO, gameUI, font);
        BuildGameOverPanel(canvasGO, gameUI, font);

        // Wire
        gameUI.cardSlots = cardSlots;
        gameUI.cardValueTexts = cardValueTexts;
        gameUI.cardBackgrounds = cardBackgrounds;
        gameUI.cardSlotRects = cardSlotRects;
        gameUI.currentPlayerText = curPlayerText;
        gameUI.roundText = roundText;
        gameUI.phaseHintText = phaseHint;
        gameUI.btnDraw = btnDraw;
        gameUI.btnTakeDiscard = btnTakeDiscard;
        gameUI.btnCallSteady = btnCallSteady;
        gameUI.btnDiscardDrawn = btnDiscardDrawn;
        gameUI.btnReplaceCard = btnReplaceCard;
        gameUI.btnUseSkill = btnUseSkill;
        gameUI.drawPileCountText = drawCount;
        gameUI.discardTopText = discardTop;
        gameUI.drawnPreview = drawnPreview;
        gameUI.drawnPreviewText = drawnPreviewText;
        gameUI.scoreTexts = scoreTexts;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[GameSceneBuilder] Game scene built (Chinese).");
    }

    #region Overlays

    static void BuildPassScreen(GameObject canvasGO, GameUI gameUI, Font font)
    {
        var ps = MakePanel(canvasGO.transform, "PassScreen", new Vector2(500, 300),
            new Color(0.02f, 0.05f, 0.1f, 0.98f));
        ps.SetActive(false);
        var pt = MakeText(ps.transform, "Txt_PassInfo", "请将设备传给\n玩家 X", 32, 400, 120, font);
        var pb = MakeButton(ps.transform, "Btn_PassReady", "准备好了！",
            new Vector2(0, -80), new Vector2(200, 60), new Color(0.15f, 0.5f, 0.2f), font);
        gameUI.passScreen = ps;
        gameUI.passScreenText = pt;
        gameUI.passScreenButton = pb;
    }

    static void BuildSkillPanel(GameObject canvasGO, GameUI gameUI, Font font)
    {
        var sp = MakePanel(canvasGO.transform, "SkillPanel", new Vector2(500, 420),
            new Color(0.08f, 0.1f, 0.2f, 0.98f));
        sp.SetActive(false);
        var st = MakeText(sp.transform, "Txt_SkillTitle", "技能", 22, 400, 40, font);
        SetPos(st.rectTransform, 0, 170);
        var ss = MakeText(sp.transform, "Txt_SkillStatus", "", 16, 400, 60, font);
        SetPos(ss.rectTransform, 0, 110);

        var slotBtns = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 90;
            slotBtns[i] = MakeButton(sp.transform, "Btn_SkillSlot" + i, "卡槽 " + (i + 1),
                new Vector2(x, 50), new Vector2(80, 45), new Color(0.2f, 0.3f, 0.4f), font);
        }
        var targetBtns = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 90;
            targetBtns[i] = MakeButton(sp.transform, "Btn_SkillTarget" + i, "玩家 " + (i + 1),
                new Vector2(x, -10), new Vector2(80, 45), new Color(0.3f, 0.2f, 0.4f), font);
        }
        var confirm = MakeButton(sp.transform, "Btn_SkillConfirm", "确认",
            new Vector2(80, -70), new Vector2(120, 45), new Color(0.15f, 0.5f, 0.2f), font);
        var decline = MakeButton(sp.transform, "Btn_SkillDecline", "取消",
            new Vector2(-80, -70), new Vector2(120, 45), new Color(0.5f, 0.2f, 0.15f), font);

        gameUI.skillPanel = sp;
        gameUI.skillTitleText = st;
        gameUI.skillStatusText = ss;
        gameUI.skillSlotBtns = slotBtns;
        gameUI.skillTargetBtns = targetBtns;
        gameUI.btnSkillConfirm = confirm;
        gameUI.btnSkillDecline = decline;
    }

    static void BuildRoundEndPanel(GameObject canvasGO, GameUI gameUI, Font font)
    {
        var rp = MakePanel(canvasGO.transform, "RoundEndPanel", new Vector2(450, 350),
            new Color(0.05f, 0.08f, 0.18f, 0.98f));
        rp.SetActive(false);
        var rt = MakeText(rp.transform, "Txt_RoundEnd", "本轮结束", 20, 400, 250, font);
        SetPos(rt.rectTransform, 0, 30);
        var rb = MakeButton(rp.transform, "Btn_RoundEnd", "下一轮",
            new Vector2(0, -120), new Vector2(200, 50), new Color(0.15f, 0.5f, 0.2f), font);
        gameUI.roundEndPanel = rp;
        gameUI.roundEndText = rt;
        gameUI.roundEndButton = rb;
    }

    static void BuildGameOverPanel(GameObject canvasGO, GameUI gameUI, Font font)
    {
        var gp = MakePanel(canvasGO.transform, "GameOverPanel", new Vector2(450, 350),
            new Color(0.08f, 0.04f, 0.08f, 0.98f));
        gp.SetActive(false);
        var gt = MakeText(gp.transform, "Txt_GameOverTitle", "游戏结束", 36, 400, 80, font);
        SetPos(gt.rectTransform, 0, 100);
        gt.color = Color.red;
        var gi = MakeText(gp.transform, "Txt_GameOverInfo", "", 20, 400, 160, font);
        SetPos(gi.rectTransform, 0, -10);
        var bb = MakeButton(gp.transform, "Btn_BackToMenu", "返回主菜单",
            new Vector2(0, -135), new Vector2(200, 50), new Color(0.2f, 0.3f, 0.5f), font);
        gameUI.gameOverPanel = gp;
        gameUI.gameOverText = gt;
        gameUI.gameOverInfoText = gi;
        gameUI.btnBackToMenu = bb;
    }

    #endregion

    #region Helpers

    static Font ResolveChineseFont()
    {
        string[] names = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "SimSun", "KaiTi", "FangSong" };
        foreach (var n in names)
        {
            try { var f = Font.CreateDynamicFontFromOSFont(n, 16); if (f != null) { Debug.Log($"[GameBuilder] Font: {n}"); return f; } }
            catch { }
        }
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { return null; }
    }

    static GameObject NewGO(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        return go;
    }

    static GameObject MakePanel(Transform parent, string name, Vector2 size, Color? color)
    {
        var go = NewGO(name, parent, size);
        if (color.HasValue) { var img = go.AddComponent<Image>(); img.color = color.Value; }
        return go;
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

    static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color color, Font font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        var lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lbl.transform.SetParent(go.transform, false);
        var lt = lbl.GetComponent<Text>();
        lt.text = label; lt.fontSize = 16; lt.alignment = TextAnchor.MiddleCenter; lt.color = Color.white;
        if (font != null) lt.font = font;
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    static void SetPos(GameObject go, float x, float y) { go.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y); }
    static void SetPos(RectTransform rt, float x, float y) { rt.anchoredPosition = new Vector2(x, y); }

    #endregion

    [MenuItem("Tools/Clear Game Scene")]
    public static void Clear()
    {
        foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "GameCanvas" || go.name == "Canvas" || go.name == "EventSystem")
                Object.DestroyImmediate(go);
        Debug.Log("[GameSceneBuilder] Cleared.");
    }
}
