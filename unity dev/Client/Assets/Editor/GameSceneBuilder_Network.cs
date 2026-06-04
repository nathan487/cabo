using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Builds the network GameScene with all player areas and cards pre-created.</summary>
public static class GameSceneBuilderNetwork
{
    [MenuItem("Tools/Build Game Scene (Network)")]
    public static void Build()
    {
        // Create or get scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera";
        cam.GetComponent<Camera>().orthographic = true;
        cam.GetComponent<Camera>().orthographicSize = 5;
        cam.transform.position = new Vector3(0, 5, -6);

        // EventSystem
        new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // Canvas
        var canvasGo = new GameObject("GameCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(800, 600);

        // ── Table Root ──
        // Note: This script builds the old uGUI-based scene.
        // For UI Toolkit version, use GameSceneBuilderUIToolkit instead.
        // Commenting out GameTableUI as it has been removed (migrated to UI Toolkit)
        var tableRoot = NewUI(canvasGo.transform, "TableRoot", new Vector2(800, 600));
        // var tableUI = tableRoot.AddComponent<Cabo.Client.Game.GameTableUI>(); // REMOVED: migrated to UI Toolkit

        // ── HUD Bar (top) ──
        var hud = MakePanel(tableRoot.transform, "HUD", new Vector2(780, 40),
            new Color(0.05f, 0.08f, 0.15f, 0.9f), new Vector2(0, 280));
        var roundInfo = MakeChineseText(hud.transform, "RoundInfo", "等待游戏开始...", 18, new Vector2(280, 32), Vector2.zero);
        var phaseText = MakeChineseText(hud.transform, "PhaseText", "", 14, new Vector2(280, 26), new Vector2(0, -16));
        phaseText.color = new Color(1f, 0.85f, 0.3f);

        // ── Pile Area (center) ──
        var pileArea = MakePanel(tableRoot.transform, "PileArea", new Vector2(300, 160), null, new Vector2(0, 50));
        var pileView = pileArea.AddComponent<Cabo.Client.Game.PileView>();

        var drawGo = MakePanel(pileArea.transform, "DrawPile", new Vector2(100, 110),
            new Color(0.15f, 0.35f, 0.55f), new Vector2(-70, 0));
        var drawTxt = MakeTMP(pileArea.transform, "DrawCount", "52", 14, new Vector2(50, 24), new Vector2(-70, -65));

        var discGo = MakePanel(pileArea.transform, "DiscardPile", new Vector2(100, 110),
            new Color(0.3f, 0.35f, 0.5f), new Vector2(70, 0));
        var discTxt = MakeTMP(pileArea.transform, "DiscardTop", "-", 22, new Vector2(50, 24), new Vector2(70, -65));

        // Wire PileView fields
        SetPrivateField(pileView, "drawPileText", drawTxt);
        SetPrivateField(pileView, "drawPileImage", drawGo.GetComponent<Image>());
        SetPrivateField(pileView, "discardPileText", discTxt);
        SetPrivateField(pileView, "discardPileImage", discGo.GetComponent<Image>());

        // ── Player Areas (Pre-created for 2 players: self + opponent) ──
        // OpponentArea: y=195, 高度=110
        var oppArea = CreatePlayerArea(tableRoot.transform, "OpponentArea", new Vector2(0, 195), false, 110);
        // SelfArea: y=-164, 高度=110
        var selfArea = CreatePlayerArea(tableRoot.transform, "SelfArea", new Vector2(0, -164), true, 110);

        // ── Action Buttons ──
        // 按钮面板：y=-251, 高度=38 (离底部30px，确保完全可见)
        var btnPanel = MakePanel(tableRoot.transform, "ActionPanel", new Vector2(760, 38),
            new Color(0.03f, 0.05f, 0.1f, 0.8f), new Vector2(0, -251));

        var btnDraw = MakeButton(btnPanel.transform, "抽牌", new Vector2(-280, 0), new Vector2(110, 34), new Color(0.15f, 0.5f, 0.6f));
        var btnTakeDisc = MakeButton(btnPanel.transform, "拿弃牌", new Vector2(-155, 0), new Vector2(110, 34), new Color(0.2f, 0.4f, 0.55f));
        var btnSteady = MakeButton(btnPanel.transform, "稳态!", new Vector2(-30, 0), new Vector2(90, 34), new Color(0.5f, 0.15f, 0.2f));
        var btnDisc = MakeButton(btnPanel.transform, "弃掉", new Vector2(80, 0), new Vector2(80, 34), new Color(0.3f, 0.3f, 0.3f));
        var btnR0 = MakeButton(btnPanel.transform, "换0", new Vector2(175, 0), new Vector2(55, 34), new Color(0.4f, 0.35f, 0.15f));
        var btnR1 = MakeButton(btnPanel.transform, "换1", new Vector2(235, 0), new Vector2(55, 34), new Color(0.4f, 0.35f, 0.15f));
        var btnR2 = MakeButton(btnPanel.transform, "换2", new Vector2(295, 0), new Vector2(55, 34), new Color(0.4f, 0.35f, 0.15f));
        var btnR3 = MakeButton(btnPanel.transform, "换3", new Vector2(355, 0), new Vector2(55, 34), new Color(0.4f, 0.35f, 0.15f));

        // ── Drawn Preview ──
        // 抽牌预览：y=50，与牌堆重叠显示（只在抽牌时出现）
        var previewGo = MakePanel(tableRoot.transform, "DrawnPreview", new Vector2(140, 45),
            new Color(0.2f, 0.25f, 0.35f, 0.95f), new Vector2(0, 50));
        var previewTxt = MakeTMP(previewGo.transform, "PreviewText", "抽到: ?", 18, new Vector2(130, 40), Vector2.zero);
        previewGo.SetActive(false);

        // ── Round End Panel ──
        var rep = MakePanel(tableRoot.transform, "RoundEndPanel", new Vector2(500, 350),
            new Color(0.05f, 0.08f, 0.18f, 0.98f), Vector2.zero);
        var repTxt = MakeChineseText(rep.transform, "RoundEndText", "", 18, new Vector2(460, 250), new Vector2(0, 20));
        var repBtn = MakeButton(rep.transform, "继续", new Vector2(0, -140), new Vector2(160, 44), new Color(0.15f, 0.5f, 0.2f));
        rep.SetActive(false);

        // ── Game Over Panel ──
        var gop = MakePanel(tableRoot.transform, "GameOverPanel", new Vector2(500, 350),
            new Color(0.08f, 0.04f, 0.08f, 0.98f), Vector2.zero);
        var gopTxt = MakeChineseText(gop.transform, "GameOverText", "", 20, new Vector2(460, 220), new Vector2(0, 30));
        var lobbyBtn = MakeButton(gop.transform, "返回大厅", new Vector2(0, -140), new Vector2(160, 44), new Color(0.2f, 0.3f, 0.5f));
        gop.SetActive(false);

        // ── Wire GameTableUI fields ──
        // REMOVED: GameTableUI has been migrated to UI Toolkit (GameTableUIToolkit)
        // This builder script is kept for reference but won't create a functional scene anymore.
        // Use GameSceneBuilderUIToolkit for the new UI Toolkit-based scene.
        /*
        SetPrivateField(tableUI, "selfArea", selfArea);
        SetPrivateField(tableUI, "opponentArea", oppArea);
        SetPrivateField(tableUI, "tableRoot", tableRoot.GetComponent<RectTransform>());
        SetPrivateField(tableUI, "roundInfoText", roundInfo);
        SetPrivateField(tableUI, "phaseText", phaseText);
        SetPrivateField(tableUI, "pileView", pileView);
        SetPrivateField(tableUI, "btnDraw", btnDraw);
        SetPrivateField(tableUI, "btnTakeDiscard", btnTakeDisc);
        SetPrivateField(tableUI, "btnCallSteady", btnSteady);
        SetPrivateField(tableUI, "btnDiscardDrawn", btnDisc);
        SetPrivateField(tableUI, "btnReplace0", btnR0);
        SetPrivateField(tableUI, "btnReplace1", btnR1);
        SetPrivateField(tableUI, "btnReplace2", btnR2);
        SetPrivateField(tableUI, "btnReplace3", btnR3);
        SetPrivateField(tableUI, "drawnPreview", previewGo);
        SetPrivateField(tableUI, "drawnPreviewText", previewTxt);
        SetPrivateField(tableUI, "roundEndPanel", rep);
        SetPrivateField(tableUI, "roundEndText", repTxt);
        SetPrivateField(tableUI, "btnRoundEndClose", repBtn);
        SetPrivateField(tableUI, "gameOverPanel", gop);
        SetPrivateField(tableUI, "gameOverText", gopTxt);
        SetPrivateField(tableUI, "btnBackToLobby", lobbyBtn);
        */

        // ── Scene Controller ──
        var ctrlGo = new GameObject("GameSceneController");
        ctrlGo.AddComponent<Cabo.Client.Game.GameSceneController>();

        // Save scene
        string path = "Assets/Scenes/GameScene.unity";
        EditorSceneManager.SaveScene(scene, path);
        Debug.Log($"[GameSceneBuilder] Scene saved: {path}");
    }

    /// <summary>Creates a complete PlayerArea with 4 CardViews pre-created.</summary>
    private static Cabo.Client.Game.PlayerAreaView CreatePlayerArea(Transform parent, string name, Vector2 pos, bool isSelf, float height = 110)
    {
        var areaGo = NewUI(parent, name, new Vector2(400, height));
        areaGo.GetComponent<RectTransform>().anchoredPosition = pos;

        // Background
        var bg = MakePanel(areaGo.transform, "Background", new Vector2(400, height),
            new Color(0.08f, 0.1f, 0.18f, 0.85f), Vector2.zero);
        var bgImage = bg.GetComponent<Image>();

        // Name text (调整位置适应新高度)
        float textOffsetY = height/2 - 15;
        var nameTxt = MakeChineseText(areaGo.transform, "NameText", isSelf ? "你" : "对手", 15,
            new Vector2(100, 22), new Vector2(-140, textOffsetY));

        // Score text
        var scoreTxt = MakeChineseText(areaGo.transform, "ScoreText", "0 分", 13,
            new Vector2(100, 18), new Vector2(-140, textOffsetY - 20));

        // Cards container (调整位置)
        var cardsContainer = NewUI(areaGo.transform, "CardsContainer", new Vector2(300, 75));
        cardsContainer.GetComponent<RectTransform>().anchoredPosition = new Vector2(30, -5);
        var horizLayout = cardsContainer.AddComponent<HorizontalLayoutGroup>();
        horizLayout.childForceExpandWidth = false;
        horizLayout.childForceExpandHeight = false;
        horizLayout.spacing = 10;
        horizLayout.childAlignment = TextAnchor.MiddleCenter;

        // Create 4 CardViews
        var cardViews = new Cabo.Client.Game.CardView[4];
        for (int i = 0; i < 4; i++)
        {
            cardViews[i] = CreateCardView(cardsContainer.transform, i);
        }

        // Add PlayerAreaView component
        var areaView = areaGo.AddComponent<Cabo.Client.Game.PlayerAreaView>();

        // Wire private fields via reflection
        SetPrivateField(areaView, "nameText", nameTxt);
        SetPrivateField(areaView, "scoreText", scoreTxt);
        SetPrivateField(areaView, "highlightBorder", bgImage);
        SetPrivateField(areaView, "cardsContainer", cardsContainer.transform);

        // Wire the CardViews array directly
        typeof(Cabo.Client.Game.PlayerAreaView)
            .GetProperty("CardViews")
            .SetValue(areaView, cardViews);

        return areaView;
    }

    /// <summary>Creates a single CardView.</summary>
    private static Cabo.Client.Game.CardView CreateCardView(Transform parent, int slotIndex)
    {
        var cardGo = NewUI(parent, $"Card_{slotIndex}", new Vector2(60, 85));

        // Background image
        var bgImage = cardGo.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.18f, 0.28f);

        // Card back (initially visible)
        var backGo = NewUI(cardGo.transform, "CardBack", new Vector2(60, 85));
        var backImage = backGo.AddComponent<Image>();
        backImage.color = new Color(0.2f, 0.25f, 0.35f);

        // Value text
        var valueTxt = MakeTMP(cardGo.transform, "ValueText", "?", 24, new Vector2(50, 50), Vector2.zero);
        valueTxt.color = Color.white;

        // Click button (covers entire card)
        var btnGo = NewUI(cardGo.transform, "ClickButton", new Vector2(60, 85));
        var btn = btnGo.AddComponent<Button>();
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0, 0, 0, 0); // Transparent

        // Add CardView component
        var cardView = cardGo.AddComponent<Cabo.Client.Game.CardView>();

        // Wire private fields
        SetPrivateField(cardView, "background", bgImage);
        SetPrivateField(cardView, "valueText", valueTxt);
        SetPrivateField(cardView, "cardBack", backImage);
        SetPrivateField(cardView, "clickButton", btn);

        return cardView;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
        }
        else
        {
            Debug.LogWarning($"[GameSceneBuilder] Field not found: {fieldName} on {target.GetType().Name}");
        }
    }


    // ── Helpers ──

    private static GameObject NewUI(Transform parent, string name, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
        return go;
    }

    private static GameObject MakePanel(Transform parent, string name, Vector2 size, Color? color, Vector2 pos)
    {
        var go = NewUI(parent, name, size);
        if (color.HasValue) { var img = go.AddComponent<Image>(); img.color = color.Value; }
        go.GetComponent<RectTransform>().anchoredPosition = pos;
        return go;
    }

    private static Font _chFont;
    private static Font GetChineseFont()
    {
        if (_chFont != null) return _chFont;
        string[] names = { "Microsoft YaHei", "SimHei", "Microsoft JhengHei", "SimSun" };
        foreach (var n in names)
            try { _chFont = Font.CreateDynamicFontFromOSFont(n, 16); if (_chFont != null) return _chFont; } catch { }
        return null;
    }

    private static TMP_Text MakeTMP(Transform parent, string name, string text, int fontSize, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fontSize; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return tmp;
    }

    private static UnityEngine.UI.Text MakeChineseText(Transform parent, string name, string text, int fontSize, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<UnityEngine.UI.Text>();
        txt.text = text; txt.fontSize = fontSize; txt.alignment = TextAnchor.MiddleCenter; txt.color = Color.white;
        txt.font = GetChineseFont();
        txt.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return txt;
    }

    private static Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        var lbl = MakeChineseText(go.transform, "Lbl", label, 14, new Vector2(size.x - 16, size.y - 8), Vector2.zero);
        return go.GetComponent<Button>();
    }
}
