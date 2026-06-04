# Game Table UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build network client game table UI with 2/3/4-player adaptive layout, card display, action buttons, and round-end/game-over panels, connected to existing ProtoGateway game events.

**Architecture:** GameScene loads, GameTableUI subscribes to GameClientController events, creates PlayerAreaViews/CardViews/PileView/GameActionPanel via MCP-generated prefabs and scripts. All views are data-driven (no hardcoded values).

**Tech Stack:** Unity 2022.3, uGUI, TextMeshPro, unity-mcp for scene building

---

## Task 1: GameScene + Bootstrap

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/GameSceneController.cs`
- Modify: `Assets/Scripts/ClientCore/Game/GameSceneBootstrap.cs`
- Modify: `Assets/Scripts/ClientCore/Game/GameClientController.cs`

**Goal:** When GameStartNotify arrives, load GameScene. GameSceneController finds ProtoGateway and GameClientController via DontDestroyOnLoad.

- [ ] **Step 1: Modify GameClientController to expose events for GameTableUI**

Add public C# events for all game notifications. Read existing file, then edit:

```csharp
// Add these events to GameClientController (existing file):
public event Action<GameStartNotify> GameStarted;
public event Action<TurnStartNotify> TurnStarted;
public event Action<ActionResultNotify> ActionReceived;
public event Action<RoundRevealNotify> RoundRevealed;
public event Action<ScoreUpdateNotify> ScoreUpdated;
public event Action<GameOverNotify> GameOvered;

// In HandleGameStart, fire event:
public void HandleGameStart(GameStartNotify notify)
{
    GameStarted?.Invoke(notify);
    // existing log...
}

// Similarly for HandleTurnStart, HandleActionResult, etc.
```

- [ ] **Step 2: Modify GameSceneBootstrap to load GameScene on game start**

```csharp
// Add to GameSceneBootstrap.cs:
using UnityEngine.SceneManagement;

public static void LoadGameScene()
{
    SceneManager.LoadScene("GameScene");
}
```

- [ ] **Step 3: Create GameSceneController**

```csharp
// Assets/Scripts/ClientCore/Game/GameSceneController.cs
using Cabo.Client.Network;
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Finds cross-scene components (ProtoGateway, GameClientController)
    /// and wires them to GameTableUI. Lives on a GameObject in GameScene.
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        public ProtoGateway Gateway { get; private set; }
        public GameClientController GameCtrl { get; private set; }

        private void Start()
        {
            // Find the DontDestroyOnLoad bootstrap
            var bootstrap = FindObjectOfType<Cabo.Client.Runtime.ClientAppBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogError("[GameSceneController] ClientAppBootstrap not found!");
                return;
            }

            Gateway = bootstrap.GetComponent<ProtoGateway>();
            GameCtrl = bootstrap.GetComponent<GameClientController>();

            // Notify GameTableUI
            var tableUI = FindObjectOfType<GameTableUI>();
            if (tableUI != null && Gateway != null && GameCtrl != null)
                tableUI.Initialize(Gateway, GameCtrl);
        }
    }
}
```

- [ ] **Step 4: Create GameScene via MCP**

Use unity-mcp to create a new empty scene:

```bash
# Via MCP: create scene
manage_scene: {"action": "create_scene", "name": "GameScene", "path": "Assets/Scenes/GameScene.unity"}
```

- [ ] **Step 5: Wire GameStartNotify → load GameScene**

In ProtoGateway.OnGameStartNotify, after receiving GameStartNotify:
```csharp
// Add to ProtoGateway.cs OnGameStartNotify:
private void OnGameStartNotify(GameStartNotify notify)
{
    // ... existing log ...
    // Queue scene load on main thread
    UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
}
```

Wait — this would load the scene but the GameTableUI hasn't been set up yet because the scene is still loading. Let me handle this differently: store the GameStartNotify data and pass it when the scene is ready.

Better approach: store GameStartNotify in a static field on GameSceneBootstrap, then GameTableUI reads it on Start.

```csharp
// In GameSceneBootstrap.cs:
public static Game.Game.GameStartNotify PendingGameStart { get; set; }

// In ProtoGateway.OnGameStartNotify:
GameSceneBootstrap.PendingGameStart = notify;
SceneManager.LoadScene("GameScene");
```

- [ ] **Step 6: Trigger Unity recompile**

```bash
refresh_unity: {"compile": "request"}
```

---

## Task 2: CardView (Prefab + Script)

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/CardView.cs`
- Create via MCP: `Assets/Prefabs/Game/CardView.prefab`

**Goal:** A single card that shows value when known, "?" when unknown. Supports simple flip animation.

- [ ] **Step 1: Create CardView script**

```csharp
// Assets/Scripts/ClientCore/Game/CardView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    public class CardView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Image cardBack;
        [SerializeField] private Button clickButton;

        [Header("Config")]
        [SerializeField] private Gradient valueColorGradient; // 0=green → 13=red

        public int SlotIndex { get; private set; }
        public bool IsKnown { get; private set; }
        public int CardValue { get; private set; }

        private Color unknownColor = new Color(0.12f, 0.18f, 0.28f);

        public void Initialize(int slotIndex)
        {
            SlotIndex = slotIndex;
            SetUnknown();
        }

        /// <summary>Show card value face-up.</summary>
        public void SetKnown(int value)
        {
            IsKnown = true;
            CardValue = value;
            valueText.text = value.ToString();
            valueText.gameObject.SetActive(true);
            if (cardBack != null) cardBack.gameObject.SetActive(false);
            if (background != null)
                background.color = EvaluateGradient(value);
        }

        /// <summary>Show card back (unknown value).</summary>
        public void SetUnknown()
        {
            IsKnown = false;
            CardValue = -1;
            valueText.text = "?";
            valueText.gameObject.SetActive(true);
            if (cardBack != null) cardBack.gameObject.SetActive(true);
            if (background != null)
                background.color = unknownColor;
        }

        /// <summary>Simple flip: scale X to 0, swap face, scale back. 0.3s total.</summary>
        public System.Collections.IEnumerator FlipToKnown(int value)
        {
            float duration = 0.15f;
            float t = 0;
            Vector3 start = transform.localScale;
            while (t < duration) { t += Time.deltaTime; transform.localScale = new Vector3(Mathf.Lerp(1, 0, t/duration), start.y, start.z); yield return null; }
            SetKnown(value);
            t = 0;
            while (t < duration) { t += Time.deltaTime; transform.localScale = new Vector3(Mathf.Lerp(0, 1, t/duration), start.y, start.z); yield return null; }
            transform.localScale = start;
        }

        public System.Collections.IEnumerator FlipToUnknown()
        {
            float duration = 0.15f;
            float t = 0;
            Vector3 start = transform.localScale;
            while (t < duration) { t += Time.deltaTime; transform.localScale = new Vector3(Mathf.Lerp(1, 0, t/duration), start.y, start.z); yield return null; }
            SetUnknown();
            t = 0;
            while (t < duration) { t += Time.deltaTime; transform.localScale = new Vector3(Mathf.Lerp(0, 1, t/duration), start.y, start.z); yield return null; }
            transform.localScale = start;
        }

        private Color EvaluateGradient(int value)
        {
            if (valueColorGradient != null) return valueColorGradient.Evaluate(value / 13f);
            float t = value / 13f;
            if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
            if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t-0.33f)/0.33f);
            return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t-0.66f)/0.34f);
        }
    }
}
```

- [ ] **Step 2: Create CardView prefab via MCP**

Execute via MCP manage_gameobject to create the prefab hierarchy. Use standard UI components:

```bash
# Create CardView GameObject with RectTransform, Image, TMP_Text, Button
# Attach CardView script
# Save as prefab to Assets/Prefabs/Game/CardView.prefab
```

Since MCP manage_gameobject can't create TMP directly, use execute_menu_item with an Editor script, or write the prefab via a Builder script.

**Better approach: Write a CardViewBuilder editor script** that creates the prefab:

```csharp
// Assets/Editor/CardViewBuilder.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CardViewBuilder
{
    [MenuItem("Tools/Build CardView Prefab")]
    public static void Build()
    {
        var go = new GameObject("CardView", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 110);

        // Background
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.12f, 0.18f, 0.28f);

        // CardBack
        var cb = new GameObject("CardBack", typeof(RectTransform), typeof(Image));
        cb.transform.SetParent(go.transform, false);
        var cbRt = cb.GetComponent<RectTransform>();
        cbRt.anchorMin = Vector2.zero; cbRt.anchorMax = Vector2.one;
        cbRt.offsetMin = new Vector2(4, 4); cbRt.offsetMax = new Vector2(-4, -4);
        cb.GetComponent<Image>().color = new Color(0.15f, 0.22f, 0.35f);

        // Value Text (TMP)
        var vt = new GameObject("ValueText", typeof(RectTransform), typeof(TextMeshProUGUI));
        vt.transform.SetParent(go.transform, false);
        var vtRt = vt.GetComponent<RectTransform>();
        vtRt.anchorMin = Vector2.zero; vtRt.anchorMax = Vector2.one;
        vtRt.offsetMin = Vector2.zero; vtRt.offsetMax = Vector2.zero;
        var tmp = vt.GetComponent<TextMeshProUGUI>();
        tmp.text = "?";
        tmp.fontSize = 32;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = Color.white;

        // Button
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg.GetComponent<Image>();

        // CardView script
        var cv = go.AddComponent<Cabo.Client.Game.CardView>();
        // SerializeField binding via reflection
        var bgField = typeof(Cabo.Client.Game.CardView).GetField("background",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bgField?.SetValue(cv, bg.GetComponent<Image>());
        var vtField = typeof(Cabo.Client.Game.CardView).GetField("valueText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        vtField?.SetValue(cv, tmp);
        var cbField = typeof(Cabo.Client.Game.CardView).GetField("cardBack",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        cbField?.SetValue(cv, cb.GetComponent<Image>());

        // Save prefab
        string path = "Assets/Prefabs/Game/CardView.prefab";
        System.IO.Directory.CreateDirectory("Assets/Prefabs/Game");
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[CardViewBuilder] Prefab created at " + path);
    }
}
```

Run via MCP: `execute_menu_item: {"menu_path": "Tools/Build CardView Prefab"}`

---

## Task 3: PlayerAreaView

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/PlayerAreaView.cs`

**Goal:** Displays one player's area: name, score, 4 CardView instances, turn highlight border.

- [ ] **Step 1: Create PlayerAreaView script**

```csharp
// Assets/Scripts/ClientCore/Game/PlayerAreaView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    public class PlayerAreaView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Image highlightBorder;
        [SerializeField] private Transform cardsContainer;

        [Header("Config")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.3f);
        [SerializeField] private Color normalColor = new Color(0.3f, 0.3f, 0.4f);

        public long PlayerId { get; private set; }
        public CardView[] CardViews { get; private set; } = new CardView[4];
        private int cardCount;

        public void Initialize(long playerId, string playerName, int initialScore)
        {
            PlayerId = playerId;
            nameText.text = playerName;
            scoreText.text = $"{initialScore}分";
            cardCount = 4;
            highlightBorder.color = normalColor;
        }

        public void SetTurnHighlight(bool isCurrentTurn)
        {
            highlightBorder.color = isCurrentTurn ? highlightColor : normalColor;
        }

        public void SetScore(int score)
        {
            scoreText.text = $"{score}分";
        }

        public void SetCardKnown(int slotIndex, int value)
        {
            if (slotIndex >= 0 && slotIndex < CardViews.Length && CardViews[slotIndex] != null)
                CardViews[slotIndex].SetKnown(value);
        }

        public void SetCardUnknown(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < CardViews.Length && CardViews[slotIndex] != null)
                CardViews[slotIndex].SetUnknown();
        }

        public void SetCardCount(int count)
        {
            cardCount = count;
            for (int i = 0; i < CardViews.Length; i++)
            {
                if (CardViews[i] != null)
                    CardViews[i].gameObject.SetActive(i < count);
            }
        }
    }
}
```

- [ ] **Step 2: Build PlayerArea via editor script (MVP: create at runtime in GameTableUI)**

For MVP simplicity, GameTableUI creates PlayerAreaViews dynamically at runtime (no separate prefab needed yet). See Task 5.

---

## Task 4: PileView

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/PileView.cs`

- [ ] **Step 1: Create PileView script**

```csharp
// Assets/Scripts/ClientCore/Game/PileView.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    public class PileView : MonoBehaviour
    {
        [SerializeField] private TMP_Text drawPileCountText;
        [SerializeField] private TMP_Text discardTopText;
        [SerializeField] private Image drawPileImage;
        [SerializeField] private Image discardPileImage;

        public void SetDrawPileCount(int count)
        {
            drawPileCountText.text = count > 0 ? $"{count}" : "0";
            drawPileImage.gameObject.SetActive(count > 0);
        }

        public void SetDiscardTop(int? value)
        {
            discardTopText.text = value.HasValue ? value.Value.ToString() : "-";
        }
    }
}
```

---

## Task 5: GameTableLayout

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/GameTableLayout.cs`

- [ ] **Step 1: Create layout calculator**

```csharp
// Assets/Scripts/ClientCore/Game/GameTableLayout.cs
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>Calculates anchored positions for player areas based on player count.</summary>
    public static class GameTableLayout
    {
        public struct Position
        {
            public Vector2 anchoredPos;
            public Vector2 size;
        }

        /// <summary>Returns layout positions indexed by seat (0=host).</summary>
        public static Position[] Calculate(int totalPlayers, int mySeat, Vector2 canvasSize)
        {
            var positions = new Position[totalPlayers];
            float areaW = 260, areaH = 100;

            switch (totalPlayers)
            {
                case 2:
                    // Self bottom, opponent top
                    positions[0] = new Position { anchoredPos = new Vector2(0, -canvasSize.y/2 + 80), size = new Vector2(areaW, areaH) };
                    positions[1] = new Position { anchoredPos = new Vector2(0, canvasSize.y/2 - 80), size = new Vector2(areaW, areaH) };
                    break;

                case 3:
                    // Self bottom, two opponents top-left and top-right
                    positions[0] = new Position { anchoredPos = new Vector2(0, -canvasSize.y/2 + 80), size = new Vector2(areaW, areaH) };
                    positions[1] = new Position { anchoredPos = new Vector2(-180, canvasSize.y/2 - 80), size = new Vector2(areaW, areaH) };
                    positions[2] = new Position { anchoredPos = new Vector2(180, canvasSize.y/2 - 80), size = new Vector2(areaW, areaH) };
                    break;

                case 4:
                    // Self bottom, top, left, right
                    positions[0] = new Position { anchoredPos = new Vector2(0, -canvasSize.y/2 + 80), size = new Vector2(areaW, areaH) };
                    positions[1] = new Position { anchoredPos = new Vector2(0, canvasSize.y/2 - 80), size = new Vector2(areaW, areaH) };
                    positions[2] = new Position { anchoredPos = new Vector2(-canvasSize.x/2 + 150, 0), size = new Vector2(areaW, areaH) };
                    positions[3] = new Position { anchoredPos = new Vector2(canvasSize.x/2 - 150, 0), size = new Vector2(areaW, areaH) };
                    break;
            }

            return positions;
        }
    }
}
```

---

## Task 6: GameTableUI (Main Controller)

**Files:**
- Create: `Assets/Scripts/ClientCore/Game/GameTableUI.cs`

- [ ] **Step 1: Create GameTableUI — all-in-one table controller**

```csharp
// Assets/Scripts/ClientCore/Game/GameTableUI.cs
using System.Collections.Generic;
using Cabo.Client.Network;
using Game.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cabo.Client.Game
{
    public class GameTableUI : MonoBehaviour
    {
        [Header("Prefab Refs")]
        [SerializeField] private GameObject cardViewPrefab;
        [SerializeField] private GameObject playerAreaPrefab;

        [Header("Canvas Refs")]
        [SerializeField] private RectTransform tableRoot;
        [SerializeField] private TMP_Text roundInfoText;
        [SerializeField] private TMP_Text phaseText;

        [Header("Pile Area")]
        [SerializeField] private TMP_Text drawPileText;
        [SerializeField] private TMP_Text discardPileText;

        [Header("Action Panel")]
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button btnDraw, btnTakeDiscard, btnCallSteady;
        [SerializeField] private Button btnDiscardDrawn, btnReplaceSlot0, btnReplaceSlot1, btnReplaceSlot2, btnReplaceSlot3;

        [Header("Drawn Card Preview")]
        [SerializeField] private GameObject drawnCardPreview;
        [SerializeField] private TMP_Text drawnCardValueText;

        [Header("Overlay")]
        [SerializeField] private GameObject roundEndPanel;
        [SerializeField] private TMP_Text roundEndText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TMP_Text gameOverText;

        private ProtoGateway gateway;
        private GameClientController gameCtrl;
        private PlayerAreaView[] playerAreas;
        private int mySeat;
        private int totalPlayers;
        private long myPlayerId;
        private bool isMyTurn;

        public void Initialize(ProtoGateway gw, GameClientController gc)
        {
            gateway = gw;
            gameCtrl = gc;
            myPlayerId = long.TryParse(gw.LocalPlayerId, out var pid) ? pid : 0;

            // Subscribe to game events
            gameCtrl.GameStarted += OnGameStarted;
            gameCtrl.TurnStarted += OnTurnStarted;
            gameCtrl.ActionReceived += OnActionReceived;
            gameCtrl.RoundRevealed += OnRoundRevealed;
            gameCtrl.ScoreUpdated += OnScoreUpdated;
            gameCtrl.GameOvered += OnGameOvered;

            // Wire action buttons
            btnDraw.onClick.AddListener(() => gateway.DrawCard());
            btnDiscardDrawn.onClick.AddListener(() => gateway.DiscardDrawn());
            btnReplaceSlot0.onClick.AddListener(() => gateway.ReplaceWithDrawn(0));
            btnReplaceSlot1.onClick.AddListener(() => gateway.ReplaceWithDrawn(1));
            btnReplaceSlot2.onClick.AddListener(() => gateway.ReplaceWithDrawn(2));
            btnReplaceSlot3.onClick.AddListener(() => gateway.ReplaceWithDrawn(3));
            btnTakeDiscard.onClick.AddListener(() => gateway.TakeFromDiscard(0));
            btnCallSteady.onClick.AddListener(() => gateway.CallSteady());

            // Load pending game start data
            var gs = GameSceneBootstrap.PendingGameStart;
            if (gs != null)
            {
                GameSceneBootstrap.PendingGameStart = null;
                OnGameStarted(gs);
            }
        }

        private void OnGameStarted(GameStartNotify gs)
        {
            totalPlayers = gs.YourView?.OpponentHands?.Count + 1 ?? 2;

            // Build player areas
            var layout = GameTableLayout.Calculate(totalPlayers, mySeat, new Vector2(800, 600));
            playerAreas = new PlayerAreaView[totalPlayers];

            // Build self
            BuildSelfArea(gs.YourView);

            // Build opponents
            int oppIdx = 0;
            foreach (var opp in gs.YourView.OpponentHands)
            {
                BuildOpponentArea(oppIdx, opp, layout);
                oppIdx++;
            }

            roundInfoText.text = $"第 {gs.RoundNumber} 轮";
            SetActionButtonsVisible(false);
        }

        private void BuildSelfArea(PlayerGameView view)
        {
            // Create self player area at bottom center
            var go = Instantiate(playerAreaPrefab, tableRoot);
            var area = go.GetComponent<PlayerAreaView>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -220);

            area.Initialize(myPlayerId, "你", view.Scores?.Find(s => s.PlayerId == myPlayerId)?.TotalScore ?? 0);

            // Create 4 card views
            for (int i = 0; i < 4; i++)
            {
                var cardGo = Instantiate(cardViewPrefab, area.transform.Find("CardsContainer"));
                var card = cardGo.GetComponent<CardView>();
                card.Initialize(i);
                
                var ownCard = view.OwnCards?.Find(c => c.SlotIndex == i);
                if (ownCard != null && ownCard.IsKnown)
                    card.SetKnown(ownCard.Value);
                else
                    card.SetUnknown();

                area.CardViews[i] = card;
            }

            // Store at position 0 (self)
            if (playerAreas != null) playerAreas[0] = area;
        }

        private void BuildOpponentArea(int idx, OpponentHandState opp, GameTableLayout.Position[] layout)
        {
            var go = Instantiate(playerAreaPrefab, tableRoot);
            var area = go.GetComponent<PlayerAreaView>();
            var rt = go.GetComponent<RectTransform>();

            int displayIdx = idx + 1; // seat index
            var pos = layout[displayIdx];
            rt.anchoredPosition = pos.anchoredPos;

            area.Initialize(opp.PlayerId, $"玩家{displayIdx+1}", 0);
            area.SetCardCount(opp.CardCount);

            // Create placeholder cards (all unknown)
            for (int i = 0; i < 4; i++)
            {
                var cardGo = Instantiate(cardViewPrefab, area.transform.Find("CardsContainer"));
                var card = cardGo.GetComponent<CardView>();
                card.Initialize(i);
                card.SetUnknown();
                card.gameObject.SetActive(i < opp.CardCount);
                area.CardViews[i] = card;
            }

            playerAreas[displayIdx] = area;
        }

        private void OnTurnStarted(TurnStartNotify ts)
        {
            isMyTurn = ts.CurrentPlayerId == myPlayerId;
            roundInfoText.text = $"第 {ts.RoundNumber} 轮 - 回合 {ts.TurnNumber}";

            // Highlight current player
            for (int i = 0; i < playerAreas.Length; i++)
                playerAreas[i]?.SetTurnHighlight(i == GetSeatIndex(ts.CurrentPlayerId));

            SetActionButtonsVisible(isMyTurn);
            phaseText.text = isMyTurn ? "轮到你行动" : $"等待玩家 {ts.CurrentPlayerId} 行动";
        }

        private void OnActionReceived(ActionResultNotify ar)
        {
            // Update pile info
            if (ar.DrawPile != null) drawPileText.text = $"{ar.DrawPile.Count}";
            if (ar.DiscardPile != null)
                discardPileText.text = ar.DiscardPile.TopCard != null ? $"{ar.DiscardPile.TopCard.Value}" : "-";

            // Update opponent card counts from exchange result
            if (ar.ExchangeResult != null && ar.TurnEnded)
            {
                var oppSeat = GetSeatIndex(ar.SourcePlayerId);
                if (oppSeat >= 0 && oppSeat < playerAreas.Length && playerAreas[oppSeat] != null)
                {
                    int newCount = 4 + ar.ExchangeResult.AddedCardCount;
                    if (ar.ExchangeResult.Success) newCount = 4; // stays at 4
                    playerAreas[oppSeat].SetCardCount(newCount);
                }
            }

            if (ar.TurnEnded)
            {
                SetActionButtonsVisible(false);
                phaseText.text = "回合结束";
            }
        }

        private void OnRoundRevealed(RoundRevealNotify rr)
        {
            roundEndPanel.SetActive(true);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"第 {rr.RoundNumber} 轮结算");
            foreach (var sc in rr.Scores)
            {
                string extra = sc.IsKamikaze ? " ⚡神风!" : (sc.Penalty > 0 ? $" [罚{sc.Penalty}分]" : "");
                sb.AppendLine($"玩家{sc.PlayerId}: 手牌{sc.HandTotal} → 本轮{sc.RoundScore}分 → 累计{sc.CumulativeScore}{extra}");
            }
            roundEndText.text = sb.ToString();
        }

        private void OnScoreUpdated(ScoreUpdateNotify su)
        {
            foreach (var sc in su.Scores)
            {
                int idx = GetSeatIndex(sc.PlayerId);
                if (idx >= 0 && idx < playerAreas.Length && playerAreas[idx] != null)
                    playerAreas[idx].SetScore(sc.TotalScore);
            }
        }

        private void OnGameOvered(GameOverNotify go)
        {
            gameOverPanel.SetActive(true);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("════ 游戏结束 ════");
            foreach (var r in go.Rankings)
            {
                string medal = r.IsWinner ? "🏆" : "";
                sb.AppendLine($"{medal} #{r.Rank} {r.Nickname}: {r.FinalScore}分");
            }
            gameOverText.text = sb.ToString();
        }

        private int GetSeatIndex(long playerId)
        {
            for (int i = 0; i < playerAreas.Length; i++)
                if (playerAreas[i]?.PlayerId == playerId) return i;
            return -1;
        }

        private void SetActionButtonsVisible(bool visible)
        {
            btnDraw.gameObject.SetActive(visible);
            btnTakeDiscard.gameObject.SetActive(visible);
            btnCallSteady.gameObject.SetActive(visible);
            btnDiscardDrawn.gameObject.SetActive(false);
            btnReplaceSlot0.gameObject.SetActive(false);
            btnReplaceSlot1.gameObject.SetActive(false);
            btnReplaceSlot2.gameObject.SetActive(false);
            btnReplaceSlot3.gameObject.SetActive(false);
        }
    }
}
```

---

## Task 7: GameScene via MCP (Editor Builder Script)

**Files:**
- Create: `Assets/Editor/GameSceneBuilder.cs`

- [ ] **Step 1: Create builder script that sets up the full GameScene**

Write an editor script to build the GameScene with all UI elements. This is similar to the existing SceneBuilder.cs pattern.

Write the complete file, then run via: `execute_menu_item: {"menu_path": "Tools/Build Game Scene (Network)"}`

---

## Task 8: Wire RoomStarted → Load GameScene

**Files:**
- Modify: `Assets/Scripts/ClientCore/Network/ProtoGateway.cs`

- [ ] **Step 1: OnGameStartNotify loads GameScene**

```csharp
private void OnGameStartNotify(GameStartNotify notify)
{
    Debug.Log($"[ProtoGateway] Game started: round={notify.RoundNumber}");
    GameSceneBootstrap.PendingGameStart = notify;
    SceneManager.LoadScene("GameScene");
}
```

---

## Task 9: Build All + Test

- [ ] **Step 1: Recompile Unity** — `refresh_unity: {"compile": "request"}`
- [ ] **Step 2: Fix any compilation errors** — read console, fix, repeat
- [ ] **Step 3: Start server + test flow** — Create room → Join → Ready → Start Game → GameScene loads → Cards visible → Turn indicator works → Action buttons work
