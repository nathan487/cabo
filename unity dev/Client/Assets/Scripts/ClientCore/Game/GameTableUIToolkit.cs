using UnityEngine;
using UnityEngine.UIElements;
using Cabo.Client.Network;
using Game.Common;
using Game.Game;

namespace Cabo.Client.Game
{
    /// <summary>
    /// UI Toolkit 版本的 GameTableUI
    /// </summary>
    public class GameTableUIToolkit : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Font chineseFont; // 直接引用字体（最可靠的方式）

        private ProtoGateway gateway;
        private VisualElement root;

        // HUD
        private Label roundInfo;
        private Label phaseText;

        // Player区域
        private Label opponentName;
        private Label opponentScore;
        private VisualElement[] opponentCards = new VisualElement[4];

        private Label selfName;
        private Label selfScore;
        private VisualElement[] selfCards = new VisualElement[4];

        // 牌堆
        private Label drawCount;
        private Label discardTop;

        // Draw预览
        private VisualElement drawnPreview;
        private Label drawnText;

        // 按钮
        private Button btnDraw;
        private Button btnTakeDiscard;
        private Button btnCallSteady;
        private Button btnDiscardDrawn;
        private Button btnReplace0;
        private Button btnReplace1;
        private Button btnReplace2;
        private Button btnReplace3;

        // 覆盖面板
        private VisualElement roundEndPanel;
        private Label roundEndText;
        private Button btnRoundEndClose;

        private VisualElement gameOverPanel;
        private Label gameOverText;
        private Button btnBackToLobby;

        private long myPlayerId;
        private long opponentPlayerId;

        // Button debouncing - prevent double clicks
        private bool isActionPending = false;

        public void Initialize(ProtoGateway gw, GameClientController gc)
        {
            gateway = gw;

            // 独立运行模式检测
            if (gw == null)
            {
                Debug.LogWarning("[GameTableUIToolkit] ProtoGateway is null - 独立 UI 测试模式");
                myPlayerId = 1; // 测试用 ID
            }
            else
            {
                Debug.Log($"[GameTableUIToolkit] LocalPlayerId string: '{gw.LocalPlayerId}'");
                bool parsed = long.TryParse(gw.LocalPlayerId, out myPlayerId);
                Debug.Log($"[GameTableUIToolkit] Parse result - Success: {parsed}, myPlayerId: {myPlayerId}");
            }

            root = uiDocument.rootVisualElement;

            // 查询所有元素
            QueryElements();

            // 绑定按钮事件
            BindButtons();

            // 订阅游戏事件（仅在有 gateway 时）
            if (gw != null)
            {
                Debug.Log("[GameTableUIToolkit] Subscribing to gateway events...");
                // Note: OnStartGame event removed - GameStartNotify consumed via PendingGameStart
                gw.OnTurnStart += OnTurnStarted;
                gw.OnActionResult += OnActionReceived;
                gw.OnRoundReveal += OnRoundRevealed;
                gw.OnScoreUpdate += OnScoreUpdated;
                gw.OnGameEnd += OnGameOvered;
                gw.OnDrawResponse += OnDrawCardRsp;
                gw.OnReplaceResponse += OnReplaceResult;
                gw.OnDiscardResponse += OnDiscardResult;
                gw.OnTakeDiscardResponse += OnTakeDiscardResult;
                Debug.Log("[GameTableUIToolkit] ✅ All events subscribed");
            }

            // IMPORTANT: Process GameStartNotify FIRST (initializes game state)
            Debug.Log($"[GameTableUIToolkit] Checking PendingGameStart...");
            Debug.Log($"[GameTableUIToolkit] GameSceneBootstrap.PendingGameStart is {(GameSceneBootstrap.PendingGameStart != null ? "NOT NULL" : "NULL")}");

            var gs = GameSceneBootstrap.PendingGameStart;
            if (gs != null)
            {
                Debug.Log($"[GameTableUIToolkit] 📥 Processing PendingGameStart (Round {gs.RoundNumber}, FirstPlayer {gs.FirstPlayerId})");
                GameSceneBootstrap.PendingGameStart = null;
                OnGameStarted(gs);
            }
            else
            {
                Debug.LogError("[GameTableUIToolkit] ❌ PendingGameStart is null! Cannot initialize game UI without game data.");
                Debug.LogError("[GameTableUIToolkit] ❌ This usually means:");
                Debug.LogError("[GameTableUIToolkit]    1. GameStartNotify was not received from server");
                Debug.LogError("[GameTableUIToolkit]    2. Scene was loaded before PendingGameStart was set");
                Debug.LogError("[GameTableUIToolkit]    3. PendingGameStart was already consumed by another instance (check for duplicate GameSceneController)");
                Debug.LogError("[GameTableUIToolkit] ❌ Check ProtoGateway logs for GameStartNotify reception.");
                Debug.LogError("[GameTableUIToolkit] ❌ If you see 'Test Mode' on screen, check if GameTableUIToolkitTester is enabled in the scene.");

                // Show error on UI instead of just returning
                if (phaseText != null)
                {
                    phaseText.text = "ERROR: Game data not loaded!";
                    phaseText.style.color = new Color(1f, 0.2f, 0.2f); // Red
                }
                if (roundInfo != null)
                {
                    roundInfo.text = "Check Console for details";
                }

                return;  // Exit early, don't initialize
            }

            // THEN process TurnStartNotify (shows turn buttons)
            if (gw != null)
            {
                var pendingTurn = gw.GetPendingTurnStart();
                if (pendingTurn != null)
                {
                    Debug.Log($"[GameTableUIToolkit] 📥 Processing cached TurnStartNotify (Turn {pendingTurn.TurnNumber})");
                    OnTurnStarted(pendingTurn);
                }
                else
                {
                    Debug.Log("[GameTableUIToolkit] No pending TurnStartNotify");
                }
            }

            Debug.Log("[GameTableUIToolkit] ✅ Initialize complete");
        }

        private void QueryElements()
        {
            // 强制使用 FontHelper（与 MainMenuScene 完全一致）
            Font font = FontHelper.GetChineseFont();

            if (font != null)
            {
                root.style.unityFont = font;
                Debug.Log($"[GameTableUIToolkit] ✅ 中文字体设置Success: {font.name}");
            }
            else
            {
                Debug.LogError("[GameTableUIToolkit] ❌ FontHelper 加载字体Failed！");
            }

            // HUD
            roundInfo = root.Q<Label>("RoundInfo");
            phaseText = root.Q<Label>("PhaseText");

            // Opponent区域
            opponentName = root.Q<Label>("OpponentName");
            opponentScore = root.Q<Label>("OpponentScore");
            for (int i = 0; i < 4; i++)
                opponentCards[i] = root.Q<VisualElement>($"OpponentCard{i}");

            // 自己区域
            selfName = root.Q<Label>("SelfName");
            selfScore = root.Q<Label>("SelfScore");
            for (int i = 0; i < 4; i++)
                selfCards[i] = root.Q<VisualElement>($"SelfCard{i}");

            // 牌堆
            drawCount = root.Q<Label>("DrawCount");
            discardTop = root.Q<Label>("DiscardTop");

            // Draw预览
            drawnPreview = root.Q<VisualElement>("DrawnPreview");
            drawnText = root.Q<Label>("DrawnText");

            // 按钮
            btnDraw = root.Q<Button>("BtnDraw");
            btnTakeDiscard = root.Q<Button>("BtnTakeDiscard");
            btnCallSteady = root.Q<Button>("BtnCallSteady");
            btnDiscardDrawn = root.Q<Button>("BtnDiscardDrawn");
            btnReplace0 = root.Q<Button>("BtnReplace0");
            btnReplace1 = root.Q<Button>("BtnReplace1");
            btnReplace2 = root.Q<Button>("BtnReplace2");
            btnReplace3 = root.Q<Button>("BtnReplace3");

            // 覆盖面板
            roundEndPanel = root.Q<VisualElement>("RoundEndPanel");
            roundEndText = root.Q<Label>("RoundEndText");
            btnRoundEndClose = root.Q<Button>("BtnRoundEndClose");

            gameOverPanel = root.Q<VisualElement>("GameOverPanel");
            gameOverText = root.Q<Label>("GameOverText");
            btnBackToLobby = root.Q<Button>("BtnBackToLobby");
        }

        private void BindButtons()
        {
            if (gateway != null)
            {
                btnDraw.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] DrawCard clicked");
                    gateway.DrawCard();
                };

                btnTakeDiscard.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] TakeFromDiscard clicked");
                    gateway.TakeFromDiscard(0);
                };

                btnCallSteady.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] CallSteady clicked");
                    gateway.CallSteady();
                };

                btnDiscardDrawn.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] DiscardDrawn clicked");
                    gateway.DiscardDrawn();
                };

                btnReplace0.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] ReplaceWithDrawn slot 0 clicked");
                    gateway.ReplaceWithDrawn(0);
                };

                btnReplace1.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] ReplaceWithDrawn slot 1 clicked");
                    gateway.ReplaceWithDrawn(1);
                };

                btnReplace2.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] ReplaceWithDrawn slot 2 clicked");
                    gateway.ReplaceWithDrawn(2);
                };

                btnReplace3.clicked += () =>
                {
                    if (isActionPending)
                    {
                        Debug.LogWarning("[GameTableUIToolkit] Action already pending, ignoring click");
                        return;
                    }
                    isActionPending = true;
                    Debug.Log("[GameTableUIToolkit] ReplaceWithDrawn slot 3 clicked");
                    gateway.ReplaceWithDrawn(3);
                };
            }
            else
            {
                // 独立测试模式：按钮仅显示，不绑定功能
                Debug.Log("[GameTableUIToolkit] 独立模式：按钮不绑定网络功能");
            }

            btnRoundEndClose.clicked += () => roundEndPanel.AddToClassList("hidden");
            btnBackToLobby.clicked += () =>
            {
                if (gateway != null) gateway.Disconnect();
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            };
        }

        private void OnGameStarted(GameStartNotify gs)
        {
            var myView = gs.YourView;

            // Update self area
            selfName.text = "You";
            selfScore.text = "0 pts";

            if (myView != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var oc = FindOwnCard(myView, i);
                    if (oc != null && oc.IsKnown)
                        SetCardKnown(selfCards[i], oc.Value);
                    else
                        SetCardUnknown(selfCards[i]);
                }
            }

            // Update opponent area
            if (myView != null && myView.OpponentHands.Count > 0)
            {
                var firstOpp = myView.OpponentHands[0];
                opponentPlayerId = firstOpp.PlayerId;
                opponentName.text = "Opponent";
                opponentScore.text = "0 pts";

                for (int i = 0; i < 4; i++)
                {
                    SetCardUnknown(opponentCards[i]);
                    opponentCards[i].style.display = i < firstOpp.CardCount ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }

            // Update piles
            drawCount.text = (gs.YourView?.DrawPile?.Count ?? 0).ToString();
            int dCount = gs.YourView?.DiscardPile?.Count ?? 0;
            discardTop.text = gs.YourView?.DiscardPile?.TopCard != null ? gs.YourView.DiscardPile.TopCard.Value.ToString() : "-";

            roundInfo.text = $"Round {gs.RoundNumber}";

            // Don't show buttons yet - wait for TurnStartNotify to avoid duplicate UI updates
            // This prevents button flicker when both GameStartNotify and TurnStartNotify arrive quickly
            HideAllButtons();
            phaseText.text = "Waiting for turn start...";
            phaseText.style.color = new Color(0.8f, 0.8f, 0.8f); // Gray
            Debug.Log($"[GameTableUIToolkit] Game initialized. Waiting for TurnStartNotify to show buttons.");

            Debug.Log("[GameTableUIToolkit] Game started, UI initialized");
        }

        private void OnTurnStarted(TurnStartNotify ts)
        {
            Debug.Log($"[GameTableUIToolkit] ========== OnTurnStarted Called ==========");
            Debug.Log($"[GameTableUIToolkit] Current Player ID: {ts.CurrentPlayerId}");
            Debug.Log($"[GameTableUIToolkit] My Player ID: {myPlayerId}");
            Debug.Log($"[GameTableUIToolkit] Is My Turn: {ts.CurrentPlayerId == myPlayerId}");
            Debug.Log($"[GameTableUIToolkit] Turn: {ts.TurnNumber}, Round: {ts.RoundNumber}");
            Debug.Log($"[GameTableUIToolkit] =============================================");

            // Check if UI objects are still valid (not destroyed during scene transition)
            if (roundInfo == null || phaseText == null)
            {
                Debug.LogWarning("[GameTableUIToolkit] ⚠️ UI objects destroyed, cannot update turn state");
                return;
            }

            // Clear action pending flag for new turn
            isActionPending = false;

            bool isMyTurn = ts.CurrentPlayerId == myPlayerId;
            roundInfo.text = $"Round {ts.RoundNumber} - Turn {ts.TurnNumber}";

            if (isMyTurn)
            {
                ShowMainButtons();
                UpdateButtonStates(); // Update based on pile availability
                phaseText.text = ">>> YOUR TURN <<<";
                phaseText.style.color = new Color(0.2f, 1f, 0.3f); // Green
                Debug.Log("[GameTableUIToolkit] ✅ Buttons shown for my turn");
            }
            else
            {
                HideAllButtons();
                phaseText.text = $"Opponent's turn (Player {ts.CurrentPlayerId})...";
                phaseText.style.color = new Color(1f, 0.8f, 0.3f); // Yellow
                Debug.Log("[GameTableUIToolkit] ❌ Buttons hidden - not my turn");
            }
        }

        private void OnActionReceived(ActionResultNotify ar)
        {
            // Update piles
            drawCount.text = (ar.DrawPile?.Count ?? 0).ToString();
            discardTop.text = ar.DiscardPile?.TopCard != null ? ar.DiscardPile.TopCard.Value.ToString() : "-";

            // Update button states based on new pile counts
            UpdateButtonStates();

            if (ar.TurnEnded)
                HideAllButtons();
        }

        /// <summary>
        /// Update button enabled/disabled state based on pile availability
        /// </summary>
        private void UpdateButtonStates()
        {
            // Check if draw pile has cards
            bool hasDrawPile = int.TryParse(drawCount.text, out int drawPileCount) && drawPileCount > 0;

            // Check if discard pile has cards
            bool hasDiscardPile = discardTop.text != "-" && !string.IsNullOrEmpty(discardTop.text);

            // Enable/disable Draw button
            btnDraw.SetEnabled(hasDrawPile);
            if (!hasDrawPile)
            {
                btnDraw.style.opacity = 0.3f;
            }
            else
            {
                btnDraw.style.opacity = 1f;
            }

            // Enable/disable Take Discard button
            btnTakeDiscard.SetEnabled(hasDiscardPile);
            if (!hasDiscardPile)
            {
                btnTakeDiscard.style.opacity = 0.3f;
            }
            else
            {
                btnTakeDiscard.style.opacity = 1f;
            }

            // Cabo button is always enabled (can always call)
            btnCallSteady.SetEnabled(true);
            btnCallSteady.style.opacity = 1f;
        }

        private void OnDrawCardRsp(DrawCardRsp rsp)
        {
            Debug.Log($"[GameTableUIToolkit] ========== OnDrawCardRsp Called ==========");
            Debug.Log($"[GameTableUIToolkit] Drew card: value={rsp.Value}, skill={rsp.Skill}");

            // Clear action pending flag
            isActionPending = false;

            drawnText.text = $"Drew: {rsp.Value}";
            if (rsp.Skill != global::Game.Common.SkillType.None)
                drawnText.text += " [Skill!]";

            drawnPreview.RemoveFromClassList("hidden");

            // Show discard and replace buttons
            ShowReplaceButtons();

            Debug.Log($"[GameTableUIToolkit] ✅ Replace buttons shown");
            Debug.Log($"[GameTableUIToolkit] BtnDiscardDrawn visible: {!btnDiscardDrawn.ClassListContains("hidden")}");
            Debug.Log($"[GameTableUIToolkit] ================================================");
        }

        private void OnReplaceResult(ReplaceWithDrawnRsp rsp)
        {
            Debug.Log($"[GameTableUIToolkit] ========== OnReplaceResult Called ==========");
            Debug.Log($"[GameTableUIToolkit] RequestId: {rsp.RequestId}");
            Debug.Log($"[GameTableUIToolkit] Success: {rsp.ExchangeResult?.Success}");

            // Clear action pending flag
            isActionPending = false;

            // Hide drawn card preview
            drawnPreview.AddToClassList("hidden");

            // Update card display if exchange succeeded
            if (rsp.ExchangeResult?.Success == true)
            {
                foreach (var si in rsp.ExchangeResult.SelectedSlotIndices)
                {
                    SetCardKnown(selfCards[si], rsp.ExchangeResult.IncomingCardValue);
                    Debug.Log($"[GameTableUIToolkit] Card at slot {si} replaced with value {rsp.ExchangeResult.IncomingCardValue}");
                }
            }

            // Hide all buttons - wait for TurnStartNotify to show appropriate buttons
            HideAllButtons();

            phaseText.text = "Waiting for next turn...";
            phaseText.style.color = new Color(0.8f, 0.8f, 0.8f); // Gray

            Debug.Log($"[GameTableUIToolkit] ✅ Replace handled, waiting for TurnStartNotify");
            Debug.Log($"[GameTableUIToolkit] ================================================");
        }

        private void OnTakeDiscardResult(TakeFromDiscardRsp rsp)
        {
            Debug.Log($"[GameTableUIToolkit] ========== OnTakeDiscardResult Called ==========");
            Debug.Log($"[GameTableUIToolkit] RequestId: {rsp.RequestId}");
            Debug.Log($"[GameTableUIToolkit] Success: {rsp.ExchangeResult?.Success}");

            // Clear action pending flag
            isActionPending = false;

            // Update card display if exchange succeeded
            if (rsp.ExchangeResult?.Success == true)
            {
                foreach (var si in rsp.ExchangeResult.SelectedSlotIndices)
                {
                    SetCardKnown(selfCards[si], rsp.ExchangeResult.IncomingCardValue);
                    Debug.Log($"[GameTableUIToolkit] Card at slot {si} replaced with discard pile card value {rsp.ExchangeResult.IncomingCardValue}");
                }
            }

            // Hide all buttons - wait for TurnStartNotify to show appropriate buttons
            HideAllButtons();

            phaseText.text = "Waiting for next turn...";
            phaseText.style.color = new Color(0.8f, 0.8f, 0.8f); // Gray

            Debug.Log($"[GameTableUIToolkit] ✅ TakeDiscard handled, waiting for TurnStartNotify");
            Debug.Log($"[GameTableUIToolkit] ================================================");
        }

        private void OnDiscardResult(DiscardDrawnRsp rsp)
        {
            Debug.Log($"[GameTableUIToolkit] ========== OnDiscardResult Called ==========");
            Debug.Log($"[GameTableUIToolkit] RequestId: {rsp.RequestId}");

            // Clear action pending flag
            isActionPending = false;

            // Hide drawn card preview
            drawnPreview.AddToClassList("hidden");

            // Hide all buttons - wait for TurnStartNotify to show appropriate buttons
            HideAllButtons();

            phaseText.text = "Waiting for next turn...";
            phaseText.style.color = new Color(0.8f, 0.8f, 0.8f); // Gray

            Debug.Log($"[GameTableUIToolkit] ✅ Discard handled, waiting for TurnStartNotify");
            Debug.Log($"[GameTableUIToolkit] ================================================");
        }

        private void OnRoundRevealed(RoundRevealNotify rr)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== Round {rr.RoundNumber} Results ===\n");

            foreach (var sc in rr.Scores)
            {
                string extra = "";
                if (sc.IsKamikaze) extra = " KAMIKAZE!";
                else if (sc.Penalty > 0) extra = $" [Penalty +{sc.Penalty}]";
                sb.AppendLine($"Player {sc.PlayerId}: Hand={sc.HandTotal} -> Round={sc.RoundScore} -> Total={sc.CumulativeScore}{extra}");
            }

            roundEndText.text = sb.ToString();
            roundEndPanel.RemoveFromClassList("hidden");
        }

        private void OnScoreUpdated(ScoreUpdateNotify su)
        {
            foreach (var sc in su.Scores)
            {
                if (sc.PlayerId == myPlayerId)
                    selfScore.text = $"{sc.TotalScore} pts";
                else if (sc.PlayerId == opponentPlayerId)
                    opponentScore.text = $"{sc.TotalScore} pts";
            }
        }

        private void OnGameOvered(GameOverNotify go)
        {
            roundEndPanel.AddToClassList("hidden");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("====== GAME OVER ======\n");

            foreach (var r in go.Rankings)
                sb.AppendLine($"{(r.IsWinner ? "WINNER" : "      ")} #{r.Rank} {r.Nickname} : {r.FinalScore} pts");

            gameOverText.text = sb.ToString();
            gameOverPanel.RemoveFromClassList("hidden");
        }

        // UI 辅助方法
        private void SetCardKnown(VisualElement card, int value)
        {
            card.RemoveFromClassList("card-back");
            card.style.backgroundColor = GetCardColor(value);
            var label = card.Q<Label>();
            if (label == null)
            {
                label = new Label(value.ToString());
                card.Add(label);
            }
            else
            {
                label.text = value.ToString();
            }
        }

        private void SetCardUnknown(VisualElement card)
        {
            card.AddToClassList("card-back");
            // 重置背景色为 USS 中定义的值（移除 inline style）
            card.style.backgroundColor = StyleKeyword.Null;
            var label = card.Q<Label>();
            if (label != null)
                label.text = "?";
        }

        private Color GetCardColor(int value)
        {
            // 恢复原始颜色算法
            float t = value / 13f;
            if (t < 0.33f) return Color.Lerp(new Color(0.1f, 0.8f, 0.2f), new Color(0.8f, 0.8f, 0.1f), t / 0.33f);
            if (t < 0.66f) return Color.Lerp(new Color(0.8f, 0.8f, 0.1f), new Color(1f, 0.4f, 0.1f), (t - 0.33f) / 0.33f);
            return Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.1f, 0.1f), (t - 0.66f) / 0.34f);
        }

        private void ShowMainButtons()
        {
            btnDraw.RemoveFromClassList("hidden");
            btnTakeDiscard.RemoveFromClassList("hidden");
            btnCallSteady.RemoveFromClassList("hidden");

            btnDiscardDrawn.AddToClassList("hidden");
            btnReplace0.AddToClassList("hidden");
            btnReplace1.AddToClassList("hidden");
            btnReplace2.AddToClassList("hidden");
            btnReplace3.AddToClassList("hidden");
        }

        private void ShowReplaceButtons()
        {
            btnDraw.AddToClassList("hidden");
            btnTakeDiscard.AddToClassList("hidden");
            btnCallSteady.AddToClassList("hidden");

            btnDiscardDrawn.RemoveFromClassList("hidden");
            btnReplace0.RemoveFromClassList("hidden");
            btnReplace1.RemoveFromClassList("hidden");
            btnReplace2.RemoveFromClassList("hidden");
            btnReplace3.RemoveFromClassList("hidden");
        }

        private void HideAllButtons()
        {
            btnDraw.AddToClassList("hidden");
            btnTakeDiscard.AddToClassList("hidden");
            btnCallSteady.AddToClassList("hidden");
            btnDiscardDrawn.AddToClassList("hidden");
            btnReplace0.AddToClassList("hidden");
            btnReplace1.AddToClassList("hidden");
            btnReplace2.AddToClassList("hidden");
            btnReplace3.AddToClassList("hidden");
        }

        private static OwnCardState FindOwnCard(PlayerGameView view, int slot)
        {
            foreach (var oc in view.OwnCards)
                if (oc.SlotIndex == slot) return oc;
            return null;
        }
    }
}
