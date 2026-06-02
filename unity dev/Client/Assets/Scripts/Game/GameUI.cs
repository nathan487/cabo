using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main game UI controller for Glucose Cabo.
/// Manages all visual elements: cards, action buttons, skill panel, turn transitions.
/// Hot-seat mode: shows "Pass to Player X" between turns.
/// All fields are wired by SceneBuilder (Editor script) — no manual inspector setup needed.
/// </summary>
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    [Header("Card Display")]
    public Button[] cardSlots;
    public Text[] cardValueTexts;
    public Image[] cardBackgrounds;
    public RectTransform[] cardSlotRects;

    [Header("Pile Display")]
    public Text drawPileCountText;
    public Text discardTopText;

    [Header("HUD")]
    public Text currentPlayerText;
    public Text roundText;
    public Text phaseHintText;

    [Header("Action Buttons")]
    public Button btnDraw;
    public Button btnTakeDiscard;
    public Button btnCallSteady;
    public Button btnDiscardDrawn;
    public Button btnReplaceCard;
    public Button btnUseSkill;
    public GameObject drawnPreview;
    public Text drawnPreviewText;

    [Header("Score Panel")]
    public Text[] scoreTexts;

    [Header("Skill Panel")]
    public GameObject skillPanel;
    public Text skillTitleText;
    public Button[] skillSlotBtns;
    public Button[] skillTargetBtns;
    public Button btnSkillConfirm;
    public Button btnSkillDecline;
    public Text skillStatusText;

    [Header("Pass Screen (Hot-Seat)")]
    public GameObject passScreen;
    public Text passScreenText;
    public Button passScreenButton;

    [Header("Round End / Game Over")]
    public GameObject roundEndPanel;
    public Text roundEndText;
    public Button roundEndButton;
    public GameObject gameOverPanel;
    public Text gameOverText;
    public Text gameOverInfoText;
    public Button btnBackToMenu;

    private CaboGameManager gm;
    private Card currentDrawnCard;

    // Opponent display (dynamically created)
    private GameObject opponentPanelRoot;
    private List<GameObject> opponentPanels = new List<GameObject>();
    private List<Text[]> opponentCardTexts = new List<Text[]>();
    private List<Image[]> opponentCardBgs = new List<Image[]>();
    private List<Text> opponentNameTexts = new List<Text>();

    // Multi-card selection state
    private List<int> selectedCardIndices = new List<int>();
    private bool multiSelectMode;

    // Skill state
    private int pendingSkillTargetPlayer = -1;
    private int pendingSkillTargetSlot = -1;
    private int pendingSkillOwnSlot = -1;
    private SkillType pendingSkillType;

    private bool isInitialized;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // All Awakes have run by now — safe to wire everything.
        Initialize();
    }

    private void OnEnable()
    {
        // If activated after Start already ran (e.g., via SetActive),
        // make sure initialization happens.
        if (!isInitialized && CaboGameManager.Instance != null)
        {
            Initialize();
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;

        gm = CaboGameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[GameUI] CaboGameManager not found!");
            return;
        }

        isInitialized = true;

        // Subscribe to game events
        gm.OnTurnStarted.AddListener(OnTurnStart);
        gm.OnCardDrawn.AddListener(OnCardDraw);
        gm.OnCardReplaced.AddListener((p, s, c) => RefreshDisplay());
        gm.OnSteadyCalled.AddListener(OnSteadyCall);
        gm.OnRoundEnded.AddListener(OnRoundEnd);
        gm.OnGameOver.AddListener(OnGameEnd);

        // Wire button listeners
        if (btnDraw != null) btnDraw.onClick.AddListener(() => gm.ActionDrawFromDeck());
        if (btnTakeDiscard != null) btnTakeDiscard.onClick.AddListener(() => gm.ActionTakeFromDiscard());
        if (btnCallSteady != null) btnCallSteady.onClick.AddListener(ShowSteadyConfirm);
        if (btnDiscardDrawn != null) btnDiscardDrawn.onClick.AddListener(() => gm.ActionDiscardDrawn());
        if (btnReplaceCard != null) btnReplaceCard.onClick.AddListener(EnterReplaceMode);
        if (btnUseSkill != null) btnUseSkill.onClick.AddListener(EnterSkillMode);
        if (passScreenButton != null) passScreenButton.onClick.AddListener(HidePassScreen);
        if (roundEndButton != null) roundEndButton.onClick.AddListener(HideRoundEnd);
        if (btnSkillConfirm != null) btnSkillConfirm.onClick.AddListener(OnSkillConfirm);
        if (btnSkillDecline != null) btnSkillDecline.onClick.AddListener(OnSkillDecline);
        if (btnBackToMenu != null) btnBackToMenu.onClick.AddListener(GoBackToMenu);

        // Wire skill panel slot buttons
        if (skillSlotBtns != null)
        {
            for (int i = 0; i < skillSlotBtns.Length && i < 4; i++)
            {
                int idx = i;
                if (skillSlotBtns[i] != null)
                    skillSlotBtns[i].onClick.AddListener(() => HandleSkillSlotClick(idx));
            }
        }

        // Wire skill panel target buttons
        if (skillTargetBtns != null)
        {
            for (int i = 0; i < skillTargetBtns.Length && i < 4; i++)
            {
                int idx = i;
                if (skillTargetBtns[i] != null)
                    skillTargetBtns[i].onClick.AddListener(() => OnSkillTargetClick(idx));
            }
        }

        // Wire card slot clicks
        if (cardSlots != null)
        {
            for (int i = 0; i < cardSlots.Length && i < 4; i++)
            {
                int idx = i;
                if (cardSlots[i] != null)
                    cardSlots[i].onClick.AddListener(() => OnCardSlotClick(idx));
            }
        }

        HideAllPanels();

        // Auto-start if player count was set by MainMenuUI
        int pendingCount = GameSceneBootstrap.PendingPlayerCount;
        if (pendingCount > 0)
        {
            GameSceneBootstrap.PendingPlayerCount = 0; // consume
            Debug.Log($"[GameUI] Auto-starting game with {pendingCount} players.");
            gm.StartNewGame(pendingCount);
        }

        CreateOpponentPanels();
    }

    // ═══════════════════════════════════════
    //  TURN FLOW
    // ═══════════════════════════════════════

    private void OnTurnStart(int playerIndex)
    {
        HideAllPanels();
        ShowPassScreen(playerIndex);
    }

    public void BeginPlayerTurn()
    {
        if (gm == null || gm.Players.Count == 0) return;

        var p = gm.Players[gm.CurrentPlayerIndex];
        currentPlayerText.text = $"{p.Name} 的回合";
        roundText.text = $"第 {gm.RoundNumber} 轮";
        phaseHintText.text = "请选择行动：";
        selectedCardIndices.Clear();
        UpdateScoreDisplay();
        RefreshCards();
        RefreshOpponentDisplay();
        UpdatePileDisplay();

        bool canDraw = gm.Deck.DrawCount > 0;
        bool canTakeDiscard = gm.Deck.TopDiscard != null;
        bool canCallSteady = !gm.FinalRoundActive;

        btnDraw.interactable = canDraw;
        btnTakeDiscard.interactable = canTakeDiscard;
        btnCallSteady.interactable = canCallSteady;
        var steadyLabel = btnCallSteady != null ? btnCallSteady.GetComponentInChildren<Text>() : null;
        if (steadyLabel != null)
            steadyLabel.text = canCallSteady ? "喊稳态！" : "（已喊过）";

        btnDiscardDrawn.gameObject.SetActive(false);
        btnReplaceCard.gameObject.SetActive(false);
        btnUseSkill.gameObject.SetActive(false);
        drawnPreview.SetActive(false);
        skillPanel.SetActive(false);
    }

    private void OnCardDraw(int playerIndex, Card card)
    {
        currentDrawnCard = card;
        drawnPreview.SetActive(true);
        drawnPreviewText.text = $"抽到：{card.Value}";

        bool fromDeck = !gm.DrewFromDiscard;
        bool hasSkill = card.IsSkillCard;

        btnDraw.interactable = false;
        btnTakeDiscard.interactable = false;
        btnCallSteady.interactable = false;

        btnDiscardDrawn.gameObject.SetActive(fromDeck);
        btnReplaceCard.gameObject.SetActive(true);
        btnReplaceCard.GetComponentInChildren<Text>().text = "替换卡牌";
        btnUseSkill.gameObject.SetActive(fromDeck && hasSkill);

        if (fromDeck)
        {
            btnReplaceCard.onClick.RemoveAllListeners();
            btnReplaceCard.onClick.AddListener(EnterReplaceMode);
        }
        else
        {
            // From discard: directly enter replace mode
            EnterReplaceMode();
        }

        phaseHintText.text = fromDeck
            ? "请选择：弃掉此牌、替换卡牌、或使用技能？"
            : "选择要替换的卡牌（可多选同值牌）：";
    }

    // ═══════════════════════════════════════
    //  CARD RENDERING
    // ═══════════════════════════════════════

    public void RefreshCards()
    {
        if (gm == null || gm.Players.Count == 0) return;
        var player = gm.Players[gm.CurrentPlayerIndex];
        int cardCount = player.Cards.Count;

        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (cardSlots[i] == null) continue;
            bool show = i < cardCount;
            cardSlots[i].gameObject.SetActive(show);

            if (!show) continue;
            if (cardValueTexts[i] == null) continue;

            bool known = player.CardKnown[i];
            cardValueTexts[i].text = known ? player.Cards[i].Value.ToString() : "?";
            cardValueTexts[i].color = known ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            cardValueTexts[i].fontSize = known ? 40 : 30;
            if (cardBackgrounds[i] != null)
                cardBackgrounds[i].color = known
                    ? ValueToColor(player.Cards[i].Value)
                    : new Color(0.12f, 0.18f, 0.28f);
        }

        // Update multi-select toggle visuals
        UpdateMultiSelectVisuals();
    }

    public void UpdatePileDisplay()
    {
        if (gm == null || gm.Deck == null) return;
        drawPileCountText.text = $"{gm.Deck.DrawCount}";
        var top = gm.Deck.TopDiscard;
        discardTopText.text = top != null ? $"{top.Value}" : "-";
    }

    public void UpdateScoreDisplay()
    {
        for (int i = 0; i < scoreTexts.Length; i++)
        {
            if (scoreTexts[i] == null) continue;
            if (i < gm.Players.Count)
            {
                var p = gm.Players[i];
                scoreTexts[i].text = $"{p.Name}: {p.TotalScore} 分";
                scoreTexts[i].color = (i == gm.CurrentPlayerIndex) ? Color.green : Color.white;
            }
            else
            {
                scoreTexts[i].text = "";
            }
        }
    }

    public void RefreshDisplay()
    {
        RefreshCards();
        RefreshOpponentDisplay();
        UpdatePileDisplay();
        UpdateScoreDisplay();
    }

    // ═══════════════════════════════════════
    //  OPPONENT DISPLAY
    // ═══════════════════════════════════════

    private void CreateOpponentPanels()
    {
        // Find the canvas to attach opponent panels
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // Remove old opponent panels if any
        foreach (var p in opponentPanels)
            if (p != null) Destroy(p);
        opponentPanels.Clear();
        opponentCardTexts.Clear();
        opponentCardBgs.Clear();
        opponentNameTexts.Clear();

        if (gm == null || gm.Players.Count <= 1) return;

        // Get a reference font from an existing text element
        Font font = null;
        if (currentPlayerText != null) font = currentPlayerText.font;

        // ── Layout: opponents sit across the top, current player at bottom ──
        // For N opponents, arrange them horizontally centered at the top.
        int oppCount = gm.Players.Count - 1;
        float panelWidth = 260f;
        float panelHeight = 70f;
        float spacing = 20f;
        float totalWidth = oppCount * panelWidth + (oppCount - 1) * spacing;
        float startX = -totalWidth / 2f + panelWidth / 2f;
        float oppY = 175f; // between HudBar (250) and PileArea (80)

        int panelIdx = 0;
        for (int i = 0; i < gm.Players.Count; i++)
        {
            if (i == gm.CurrentPlayerIndex) continue;
            var opponent = gm.Players[i];

            float xPos = startX + panelIdx * (panelWidth + spacing);

            // Panel container (semi-transparent background)
            var panelGo = new GameObject("OpponentPanel_" + i, typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(canvas.transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRt.anchoredPosition = new Vector2(xPos, oppY);
            panelGo.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.15f, 0.85f);

            // Opponent name + score at top-left
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(panelGo.transform, false);
            var nameText = nameGo.GetComponent<Text>();
            nameText.text = $"{opponent.Name}  {opponent.TotalScore}分";
            nameText.fontSize = 12;
            nameText.color = new Color(0.7f, 0.7f, 0.9f);
            nameText.alignment = TextAnchor.MiddleCenter;
            if (font != null) nameText.font = font;
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0, -12);
            nameRt.sizeDelta = new Vector2(panelWidth - 10, 20);

            // 4 card slots centered in panel
            var cardTexts = new Text[4];
            var cardBgs = new Image[4];
            float cardWidth = 50f;
            float cardHeight = 44f;
            float cardSpacing = 6f;
            float cardsTotalWidth = 4 * cardWidth + 3 * cardSpacing;
            float cardStartX = -cardsTotalWidth / 2f + cardWidth / 2f;

            for (int c = 0; c < 4; c++)
            {
                var slotGo = new GameObject("CardSlot_" + c, typeof(RectTransform), typeof(Image));
                slotGo.transform.SetParent(panelGo.transform, false);
                var slotRt = slotGo.GetComponent<RectTransform>();
                slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(cardStartX + c * (cardWidth + cardSpacing), 2);
                slotRt.sizeDelta = new Vector2(cardWidth, cardHeight);
                slotGo.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.28f);

                // Card value text
                var txtGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
                txtGo.transform.SetParent(slotGo.transform, false);
                var txt = txtGo.GetComponent<Text>();
                txt.text = "?";
                txt.fontSize = 18;
                txt.color = new Color(0.5f, 0.5f, 0.5f);
                txt.alignment = TextAnchor.MiddleCenter;
                if (font != null) txt.font = font;
                var txtRt = txtGo.GetComponent<RectTransform>();
                txtRt.anchorMin = Vector2.zero;
                txtRt.anchorMax = Vector2.one;
                txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;

                cardTexts[c] = txt;
                cardBgs[c] = slotGo.GetComponent<Image>();
            }

            opponentPanels.Add(panelGo);
            opponentCardTexts.Add(cardTexts);
            opponentCardBgs.Add(cardBgs);
            opponentNameTexts.Add(nameText);
            panelIdx++;
        }
    }

    private void RefreshOpponentDisplay()
    {
        if (gm == null || gm.Players.Count <= 1) return;
        var currentPlayer = gm.Players[gm.CurrentPlayerIndex];

        int panelIdx = 0;
        for (int i = 0; i < gm.Players.Count; i++)
        {
            if (i == gm.CurrentPlayerIndex) continue;
            if (panelIdx >= opponentPanels.Count) break;

            var opponent = gm.Players[i];
            var panel = opponentPanels[panelIdx];
            var cardTexts = opponentCardTexts[panelIdx];
            var cardBgs = opponentCardBgs[panelIdx];

            // Update name + score
            if (panelIdx < opponentNameTexts.Count && opponentNameTexts[panelIdx] != null)
                opponentNameTexts[panelIdx].text = $"{opponent.Name}  {opponent.TotalScore}分";

            // Show panel
            panel.SetActive(true);

            // Show opponent cards (what does currentPlayer know about each?)
            for (int c = 0; c < 4 && c < opponent.Cards.Count; c++)
            {
                if (cardTexts[c] == null) continue;

                bool known = currentPlayer.OpponentKnown.ContainsKey(i)
                          && c < currentPlayer.OpponentKnown[i].Count
                          && currentPlayer.OpponentKnown[i][c];

                int knownVal = known
                    ? (currentPlayer.OpponentValues.ContainsKey(i)
                       && c < currentPlayer.OpponentValues[i].Count
                       ? currentPlayer.OpponentValues[i][c] : -1)
                    : -1;

                if (known && knownVal >= 0)
                {
                    cardTexts[c].text = knownVal.ToString();
                    cardTexts[c].color = Color.white;
                    cardTexts[c].fontSize = 20;
                    cardBgs[c].color = ValueToColor(knownVal);
                }
                else
                {
                    cardTexts[c].text = "?";
                    cardTexts[c].color = new Color(0.4f, 0.4f, 0.45f);
                    cardTexts[c].fontSize = 16;
                    cardBgs[c].color = new Color(0.1f, 0.15f, 0.22f);
                }
            }
            panelIdx++;
        }

        // Hide unused panels
        for (int j = panelIdx; j < opponentPanels.Count; j++)
        {
            if (opponentPanels[j] != null)
                opponentPanels[j].SetActive(false);
        }
    }

    private Color ValueToColor(int value)
    {
        float t = value / 13f;
        if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
        else if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t - 0.33f) / 0.33f);
        else return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t - 0.66f) / 0.34f);
    }

    // ═══════════════════════════════════════
    //  CARD SLOT CLICK
    // ═══════════════════════════════════════

    private void OnCardSlotClick(int slotIndex)
    {
        if (gm.CurrentPhase == TurnPhase.ChoosingReplaceSlot)
        {
            ToggleCardSelection(slotIndex);
        }
        else if (gm.CurrentPhase == TurnPhase.SkillActive)
        {
            HandleSkillSlotClick(slotIndex);
        }
    }

    private void EnterReplaceMode()
    {
        gm.CurrentPhase = TurnPhase.ChoosingReplaceSlot;
        selectedCardIndices.Clear();
        phaseHintText.text = "选择要替换的卡牌（可多选同值牌），然后确认：";

        // Repurpose btnReplaceCard as confirm button
        btnReplaceCard.gameObject.SetActive(true);
        btnReplaceCard.GetComponentInChildren<Text>().text = "确认替换";
        btnReplaceCard.onClick.RemoveAllListeners();
        btnReplaceCard.onClick.AddListener(ConfirmReplaceSelection);

        btnDiscardDrawn.gameObject.SetActive(false);
        btnUseSkill.gameObject.SetActive(false);
        UpdateMultiSelectVisuals();
    }

    /// <summary>
    /// Toggle a card index in the multi-select list.
    /// </summary>
    private void ToggleCardSelection(int slotIndex)
    {
        var player = gm.Players[gm.CurrentPlayerIndex];
        if (slotIndex < 0 || slotIndex >= player.Cards.Count) return;

        if (selectedCardIndices.Contains(slotIndex))
            selectedCardIndices.Remove(slotIndex);
        else
            selectedCardIndices.Add(slotIndex);

        UpdateMultiSelectVisuals();
    }

    /// <summary>
    /// Execute the replacement with currently selected cards.
    /// </summary>
    public void ConfirmReplaceSelection()
    {
        if (selectedCardIndices.Count == 0) return;

        if (selectedCardIndices.Count == 1)
        {
            gm.ActionReplaceCard(selectedCardIndices[0]);
        }
        else
        {
            gm.ActionReplaceMultipleCards(new List<int>(selectedCardIndices));
        }
        selectedCardIndices.Clear();
    }

    private void UpdateMultiSelectVisuals()
    {
        var player = gm != null && gm.Players.Count > 0 ? gm.Players[gm.CurrentPlayerIndex] : null;
        for (int i = 0; i < cardSlots.Length && i < 4; i++)
        {
            if (cardSlots[i] == null || !cardSlots[i].gameObject.activeSelf) continue;
            var img = cardSlots[i].GetComponent<Image>();
            if (img == null) continue;

            if (selectedCardIndices.Contains(i))
            {
                // Highlight selected card
                img.color = new Color(0.3f, 0.6f, 0.9f);
            }
            else if (player != null && i < player.Cards.Count && player.CardKnown[i])
            {
                img.color = ValueToColor(player.Cards[i].Value);
            }
            else
            {
                img.color = new Color(0.12f, 0.18f, 0.28f);
            }
        }
    }

    /// <summary>
    /// External entry point for card replacement (used by CardDisplay prefab).
    /// </summary>
    public void ReplaceCardExternal(int slotIndex)
    {
        if (gm.CurrentPhase == TurnPhase.ChoosingReplaceSlot)
            ToggleCardSelection(slotIndex);
    }

    // ═══════════════════════════════════════
    //  SKILL SYSTEM
    // ═══════════════════════════════════════

    private void EnterSkillMode()
    {
        if (currentDrawnCard == null || !currentDrawnCard.IsSkillCard) return;

        gm.CurrentPhase = TurnPhase.SkillActive;
        pendingSkillType = currentDrawnCard.Skill;
        pendingSkillOwnSlot = -1;
        pendingSkillTargetPlayer = -1;
        pendingSkillTargetSlot = -1;

        skillPanel.SetActive(true);
        skillStatusText.text = "";

        // Restore correct confirm/decline listeners (may have been overwritten by ShowSteadyConfirm)
        btnSkillConfirm.onClick.RemoveAllListeners();
        btnSkillConfirm.onClick.AddListener(OnSkillConfirm);
        btnSkillDecline.onClick.RemoveAllListeners();
        btnSkillDecline.onClick.AddListener(OnSkillDecline);

        // Hide all by default
        SetSkillSlotsVisible(false);
        SetSkillTargetsVisible(false);
        btnSkillConfirm.gameObject.SetActive(false);
        btnSkillDecline.gameObject.SetActive(false);

        switch (pendingSkillType)
        {
            case SkillType.PeekSelf:
                skillTitleText.text = "偷看（查看自己一张牌）";
                skillStatusText.text = "选择你的一张卡：";
                SetSkillSlotsVisible(true);
                break;
            case SkillType.Spy:
                skillTitleText.text = "侦查（查看对手一张牌）";
                skillStatusText.text = "选择目标玩家：";
                SetSkillTargetsVisible(true);
                break;
            case SkillType.BlindSwap:
                skillTitleText.text = "盲换";
                skillStatusText.text = "先选择你的一张卡：";
                SetSkillSlotsVisible(true);
                break;
        }
    }

    private void SetSkillSlotsVisible(bool visible)
    {
        foreach (var b in skillSlotBtns)
            if (b != null) b.gameObject.SetActive(visible);
    }

    private void SetSkillTargetsVisible(bool visible)
    {
        for (int i = 0; i < skillTargetBtns.Length; i++)
        {
            if (skillTargetBtns[i] == null) continue;
            skillTargetBtns[i].gameObject.SetActive(visible && i < gm.Players.Count
                && i != gm.CurrentPlayerIndex);
            if (visible && i < gm.Players.Count)
                skillTargetBtns[i].GetComponentInChildren<Text>().text = gm.Players[i].Name;
        }
    }

    private void HandleSkillSlotClick(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= gm.Players[gm.CurrentPlayerIndex].Cards.Count) return;

        switch (pendingSkillType)
        {
            case SkillType.PeekSelf:
                gm.ExecutePeekSelf(slotIndex);
                skillPanel.SetActive(false);
                break;

            case SkillType.BlindSwap:
                if (pendingSkillOwnSlot < 0)
                {
                    pendingSkillOwnSlot = slotIndex;
                    skillStatusText.text = $"你的卡槽 {slotIndex + 1}。现在选择对手：";
                    SetSkillSlotsVisible(false);
                    SetSkillTargetsVisible(true);
                }
                else
                {
                    pendingSkillTargetSlot = slotIndex;
                    skillStatusText.text = $"用你的卡槽 {pendingSkillOwnSlot + 1} 与 {gm.Players[pendingSkillTargetPlayer].Name} 的卡槽 {slotIndex + 1} 交换？";
                    btnSkillConfirm.gameObject.SetActive(true);
                }
                break;

            case SkillType.Spy:
                if (pendingSkillTargetPlayer >= 0)
                {
                    pendingSkillTargetSlot = slotIndex;
                    gm.ExecuteSpy(pendingSkillTargetPlayer, slotIndex);
                    skillPanel.SetActive(false);
                }
                break;
        }
    }

    private void OnSkillTargetClick(int playerIndex)
    {
        pendingSkillTargetPlayer = playerIndex;
        var target = gm.Players[playerIndex];

        switch (pendingSkillType)
        {
            case SkillType.Spy:
                skillStatusText.text = $"目标：{target.Name}。选择一个卡槽：";
                SetSkillTargetsVisible(false);
                SetSkillSlotsVisible(true);
                break;
            case SkillType.BlindSwap:
                skillStatusText.text = $"对手：{target.Name}。选择其卡槽：";
                SetSkillTargetsVisible(false);
                SetSkillSlotsVisible(true);
                break;
        }
    }

    private void OnSkillConfirm()
    {
        switch (pendingSkillType)
        {
            case SkillType.BlindSwap:
                if (pendingSkillOwnSlot >= 0 && pendingSkillTargetPlayer >= 0 && pendingSkillTargetSlot >= 0)
                    gm.ExecuteBlindSwap(pendingSkillOwnSlot, pendingSkillTargetPlayer, pendingSkillTargetSlot);
                break;
        }
        skillPanel.SetActive(false);
    }

    private void OnSkillDecline()
    {
        // Cancel skill — go back to decision phase (player can still discard or replace)
        skillPanel.SetActive(false);
        gm.CurrentPhase = TurnPhase.DecidingDrawnCard;
        phaseHintText.text = "请选择：弃掉此牌、替换卡牌、或使用技能？";
    }

    // ═══════════════════════════════════════
    //  STEADY
    // ═══════════════════════════════════════

    private void ShowSteadyConfirm()
    {
        skillPanel.SetActive(true);
        skillTitleText.text = "确认喊稳态？";
        skillStatusText.text = "结束本轮，其余玩家各还有一次回合。";
        SetSkillSlotsVisible(false);
        SetSkillTargetsVisible(false);
        btnSkillConfirm.gameObject.SetActive(true);
        btnSkillDecline.gameObject.SetActive(true);
        btnSkillConfirm.GetComponentInChildren<Text>().text = "确认稳态！";
        btnSkillDecline.GetComponentInChildren<Text>().text = "Cancel";

        btnSkillConfirm.onClick.RemoveAllListeners();
        btnSkillConfirm.onClick.AddListener(() =>
        {
            skillPanel.SetActive(false);
            gm.ActionCallSteady();
        });
        btnSkillDecline.onClick.RemoveAllListeners();
        btnSkillDecline.onClick.AddListener(() =>
        {
            skillPanel.SetActive(false);
            BeginPlayerTurn();
        });
    }

    private void OnSteadyCall()
    {
        phaseHintText.text = $"{gm.Players[gm.SteadyCallerIndex].Name} 喊了稳态！";
    }

    // ═══════════════════════════════════════
    //  PASS SCREEN
    // ═══════════════════════════════════════

    private void ShowPassScreen(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= gm.Players.Count) return;
        var player = gm.Players[playerIndex];
        passScreen.SetActive(true);
        passScreenText.text = $"请将设备传给\n{player.Name}";
    }

    public void HidePassScreen()
    {
        passScreen.SetActive(false);
        BeginPlayerTurn();
    }

    // ═══════════════════════════════════════
    //  ROUND END / GAME OVER
    // ═══════════════════════════════════════

    private void OnRoundEnd(int roundNum, int steadyCaller)
    {
        roundEndPanel.SetActive(true);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"第 {roundNum} 轮结束！");
        sb.AppendLine($"{gm.Players[steadyCaller].Name} 喊了稳态");
        sb.AppendLine();
        foreach (var p in gm.Players)
            sb.AppendLine($"{p.Name}: 本轮 {p.GetRoundScore()} 分 → 累计 {p.TotalScore} 分");
        roundEndText.text = sb.ToString();
    }

    public void HideRoundEnd()
    {
        roundEndPanel.SetActive(false);
        if (!gm.GameOver)
            OnTurnStart(gm.CurrentPlayerIndex);
    }

    private void OnGameEnd(Player winner)
    {
        roundEndPanel.SetActive(false);
        gameOverPanel.SetActive(true);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("游戏结束");
        sb.AppendLine($"胜者：{winner.Name}");
        sb.AppendLine();
        foreach (var p in gm.Players)
            sb.AppendLine($"{p.Name}: {p.TotalScore} 分");
        gameOverInfoText.text = sb.ToString();
    }

    // ═══════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════

    public void GoBackToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void HideAllPanels()
    {
        if (passScreen != null) passScreen.SetActive(false);
        if (skillPanel != null) skillPanel.SetActive(false);
        if (drawnPreview != null) drawnPreview.SetActive(false);
        if (roundEndPanel != null) roundEndPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }
}
