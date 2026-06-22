using UnityEngine;
using UnityEngine.UIElements;
using Cabo.Client.Art;
using Cabo.Client.UI.CardTable;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Root UI Manager. Owns the UIDocument and routes between panels.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        const float RevealAnimationDrainTimeoutMargin = 2.0f;
        public static float RevealAnimationDrainTimeoutSeconds =>
            GameTablePanel.LongestRevealBlockingActionDurationSeconds
            + GameTablePanel.PlaybackLayoutSettleDelaySeconds
            + RevealAnimationDrainTimeoutMargin;

        [SerializeField] public UIDocument uiDocument;

        public VisualElement Root { get; private set; }
        public RoomPanel RoomPanel { get; private set; }
        public GameTablePanel GameTablePanel { get; private set; }

        GameFlow _flow;
        VisualElement _backgroundLayer;
        VisualElement _reconnectOverlay;
        Label _reconnectLabel;
        Button _debugDisconnectButton;
        System.Action<Game.Messages.ServerMessage> _messageReceivedHandler;
        bool _waitingForRevealAnimationDrain;
        float _revealAnimationDrainStartedAt;

        void Awake()
        {
            // uiDocument is set by GameBootstrap after AddComponent
            // Don't access rootVisualElement here; defer to Initialize().
        }

        void EnsureRoot()
        {
            if (Root != null) return;
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) uiDocument = GetComponentInChildren<UIDocument>();
            if (uiDocument != null)
            {
                Root = uiDocument.rootVisualElement;
                Root.Clear();
                Root.style.flexGrow = 1;
                Root.style.width = Length.Percent(100);
                Root.style.height = Length.Percent(100);
                Root.style.position = Position.Absolute;
                Root.style.left = 0;
                Root.style.right = 0;
                Root.style.top = 0;
                Root.style.bottom = 0;
                UITheme.ApplyRoot(Root);
                Debug.Log($"[UIManager] Root ready: {Root != null}, childCount={Root?.childCount}, panelSettings={uiDocument.panelSettings != null}");
            }
            if (Root == null) Debug.LogError("[UIManager] Cannot find UIDocument!");
        }

        public void Initialize(GameFlow flow)
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
                if (_messageReceivedHandler != null)
                    _flow.Gateway.MessageReceived -= _messageReceivedHandler;
            }

            _flow = flow;
            EnsureRoot();
            GameTablePanel?.Dispose();
            GameTablePanel = null;
            Root?.Clear();
            CardTableView.DestroyAllUnder(transform);

            CreateBackgroundLayer();

            // Build panels
            RoomPanel = new RoomPanel(Root, flow);
            GameTablePanel = new GameTablePanel(Root, flow, transform);
            GameTablePanel.SetAnimationQueueDrainedCallback(OnStateChanged);
            CreateReconnectOverlay();
            CreateReconnectDebugButton();

            // Listen for state changes
            flow.StateChanged += OnStateChanged;
            _messageReceivedHandler = _ => OnStateChanged();
            flow.Gateway.MessageReceived += _messageReceivedHandler;

            // Initial render
            OnStateChanged();
        }

        void Update()
        {
            if (!_waitingForRevealAnimationDrain || _flow == null || GameTablePanel == null)
                return;

            var state = _flow.State;
            bool revealPending = state.Phase == GamePhase.RoundReveal
                || state.RoundJustRevealed
                || _flow.Flow == FlowState.RoundReveal;
            if (!revealPending)
            {
                _waitingForRevealAnimationDrain = false;
                return;
            }

            if (!GameTablePanel.HasPendingActionAnimation)
            {
                _waitingForRevealAnimationDrain = false;
                _revealAnimationDrainStartedAt = 0f;
                OnStateChanged();
                return;
            }

            if (_revealAnimationDrainStartedAt <= 0f)
                _revealAnimationDrainStartedAt = Time.realtimeSinceStartup;

            if (Time.realtimeSinceStartup - _revealAnimationDrainStartedAt >= RevealAnimationDrainTimeoutSeconds)
            {
                Debug.LogWarning("[UIManager] Reveal animation drain timed out; forcing settlement render.");
                GameTablePanel.ForceCompletePendingActionAnimationForReveal();
                _waitingForRevealAnimationDrain = false;
                _revealAnimationDrainStartedAt = 0f;
                OnStateChanged();
            }
        }

        void OnStateChanged()
        {
            var state = _flow.State;
            Debug.Log($"[UIManager] OnStateChanged: phase={state.Phase}, flow={_flow.Flow}, players={state.Players.Count}, room={state.RoomCode}");

            // Show/hide panels based on flow state
            bool showOver = state.Phase == GamePhase.GameOver;
            bool revealPending = !showOver && (state.Phase == GamePhase.RoundReveal || state.RoundJustRevealed || _flow.Flow == FlowState.RoundReveal);
            bool waitForActionAnimation = revealPending && GameTablePanel.HasPendingActionAnimation;
            if (waitForActionAnimation && !_waitingForRevealAnimationDrain)
                _revealAnimationDrainStartedAt = Time.realtimeSinceStartup;
            if (!waitForActionAnimation)
                _revealAnimationDrainStartedAt = 0f;
            _waitingForRevealAnimationDrain = waitForActionAnimation;
            bool showReveal = revealPending && !waitForActionAnimation;
            bool showGame = !showReveal && !showOver && (_flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing);
            if (waitForActionAnimation)
                showGame = true;
            bool showRoomPanel = _flow.Flow == FlowState.Home
                || _flow.Flow == FlowState.Connecting
                || _flow.Flow == FlowState.RoomFlow
                || _flow.Flow == FlowState.RoomBrowser
                || _flow.Flow == FlowState.WaitingRoom
                || state.Phase == GamePhase.WaitingRoom;

            ApplyScreenBackground(showReveal || showOver
                ? CaboArt.SettlementBackground
                : showGame ? CaboArt.TableBackground : CaboArt.HomeBackground);

            RoomPanel.SetVisible(showRoomPanel && !showGame && !showReveal && !showOver);
            GameTablePanel.SetVisible(showGame || showReveal || showOver);

            if (showRoomPanel && !showReveal && !showOver) RoomPanel.Render();
            if (showGame) GameTablePanel.RenderGame();
            if (showReveal) GameTablePanel.RenderReveal();
            if (showOver) GameTablePanel.RenderGameOver();
            UpdateReconnectDebugButton();
            UpdateReconnectOverlay();

            ApplyRuntimeUiFallback();
        }

        void CreateBackgroundLayer()
        {
            if (Root == null)
                return;

            _backgroundLayer = new VisualElement { name = "CaboScreenBackground" };
            _backgroundLayer.style.position = Position.Absolute;
            _backgroundLayer.style.left = 0;
            _backgroundLayer.style.right = 0;
            _backgroundLayer.style.top = 0;
            _backgroundLayer.style.bottom = 0;
            _backgroundLayer.style.backgroundColor = UITheme.AppBackground;
            _backgroundLayer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            _backgroundLayer.pickingMode = PickingMode.Ignore;
            Root.Add(_backgroundLayer);
        }

        void CreateReconnectOverlay()
        {
            if (Root == null)
                return;

            _reconnectOverlay = new VisualElement { name = "CaboReconnectOverlay" };
            _reconnectOverlay.style.position = Position.Absolute;
            _reconnectOverlay.style.left = 0;
            _reconnectOverlay.style.right = 0;
            _reconnectOverlay.style.top = 0;
            _reconnectOverlay.style.bottom = 0;
            _reconnectOverlay.style.alignItems = Align.Center;
            _reconnectOverlay.style.justifyContent = Justify.Center;
            _reconnectOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _reconnectOverlay.style.display = DisplayStyle.None;
            _reconnectOverlay.pickingMode = PickingMode.Position;

            _reconnectLabel = new Label("正在重连...");
            _reconnectLabel.style.paddingLeft = 18;
            _reconnectLabel.style.paddingRight = 18;
            _reconnectLabel.style.paddingTop = 10;
            _reconnectLabel.style.paddingBottom = 10;
            _reconnectLabel.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            _reconnectLabel.style.color = Color.white;
            _reconnectLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _reconnectOverlay.Add(_reconnectLabel);
            Root.Add(_reconnectOverlay);
        }

        void UpdateReconnectOverlay()
        {
            if (_reconnectOverlay == null || _flow == null)
                return;

            _reconnectOverlay.style.display = _flow.IsReconnecting ? DisplayStyle.Flex : DisplayStyle.None;
            if (_reconnectLabel != null)
                _reconnectLabel.text = string.IsNullOrEmpty(_flow.LastConnectError)
                    ? "正在重连..."
                    : _flow.LastConnectError;
            if (_flow.IsReconnecting)
                _reconnectOverlay.BringToFront();
        }

        void CreateReconnectDebugButton()
        {
            if (Root == null || !ShouldShowReconnectDebugButton(Application.isEditor, Debug.isDebugBuild))
                return;

            _debugDisconnectButton = new Button(OnReconnectDebugDisconnectClicked)
            {
                name = "CaboReconnectDebugDisconnectButton",
                text = "模拟断线",
                tooltip = "断开当前连接，用于测试自动重连"
            };
            _debugDisconnectButton.style.position = Position.Absolute;
            _debugDisconnectButton.style.top = 12;
            _debugDisconnectButton.style.right = 12;
            _debugDisconnectButton.style.minWidth = 86;
            _debugDisconnectButton.style.height = 32;
            _debugDisconnectButton.style.paddingLeft = 10;
            _debugDisconnectButton.style.paddingRight = 10;
            _debugDisconnectButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            UITheme.ApplyButton(_debugDisconnectButton, true);
            Root.Add(_debugDisconnectButton);
        }

        void UpdateReconnectDebugButton()
        {
            if (_debugDisconnectButton == null || _flow == null)
                return;

            bool shouldShow = ShouldShowReconnectDebugButton(Application.isEditor, Debug.isDebugBuild)
                && !_flow.IsReconnecting;
            _debugDisconnectButton.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
            if (!shouldShow)
                return;

            bool enabled = ShouldEnableReconnectDebugButton(
                _flow.IsConnected,
                _flow.IsReconnecting,
                _flow.State.SessionToken,
                _flow.State.MyPlayerId,
                _flow.State.RoomId);
            _debugDisconnectButton.SetEnabled(enabled);
            _debugDisconnectButton.tooltip = enabled
                ? "断开当前连接，用于测试自动重连"
                : "进入房间并拿到 session_token 后可模拟断线";
            _debugDisconnectButton.BringToFront();
        }

        void OnReconnectDebugDisconnectClicked()
        {
            if (_flow == null || !ShouldEnableReconnectDebugButton(
                    _flow.IsConnected,
                    _flow.IsReconnecting,
                    _flow.State.SessionToken,
                    _flow.State.MyPlayerId,
                    _flow.State.RoomId))
                return;

            Debug.Log("[UIManager] Simulating connection drop for reconnect test.");
            _flow.Gateway.Disconnect();
            UpdateReconnectDebugButton();
        }

        public static bool ShouldShowReconnectDebugButton(bool isEditor, bool isDevelopmentBuild)
        {
            return isEditor || isDevelopmentBuild;
        }

        public static bool ShouldEnableReconnectDebugButton(
            bool isConnected,
            bool isReconnecting,
            string sessionToken,
            long playerId,
            long roomId)
        {
            return isConnected
                && !isReconnecting
                && !string.IsNullOrWhiteSpace(sessionToken)
                && playerId > 0
                && roomId > 0;
        }

        void ApplyScreenBackground(Sprite sprite)
        {
            if (_backgroundLayer == null)
                return;

            _backgroundLayer.style.backgroundImage = sprite != null
                ? new StyleBackground(sprite)
                : new StyleBackground(StyleKeyword.None);
        }

        void ApplyRuntimeUiFallback()
        {
            if (Root == null)
                return;

            UITheme.ApplyRoot(Root);

            Root.Query<Label>().ForEach(label =>
            {
                if (label.ClassListContains(UITheme.TitleTextClass) || label.resolvedStyle.fontSize >= 20f)
                    UITheme.ApplyTitle(label);
                else
                    UITheme.ApplyBodyFont(label);
                if (IsOnBrightSurface(label))
                    return;
                label.style.color = UITheme.TextPrimary;
            });

            Root.Query<Button>().ForEach(button =>
            {
                ApplyReadableButtonStyle(button, button.enabledSelf);
            });

            Root.Query<TextField>().ForEach(field =>
            {
                UITheme.ApplyBodyFont(field);
                EnsureImeSupport(field);
            });
        }

        public static void ApplyReadableButtonStyle(Button button, bool enabled)
        {
            if (button == null)
                return;

            UITheme.ApplyButton(button, enabled);
        }

        static bool IsOnBrightSurface(VisualElement element)
        {
            for (var current = element.parent; current != null; current = current.parent)
            {
                var bg = current.resolvedStyle.backgroundColor;
                if (bg.a > 0.2f && (bg.r + bg.g + bg.b) / 3f > 0.55f)
                    return true;
            }
            return false;
        }

        static void EnsureImeSupport(TextField field)
        {
            const string boundClass = "cabo-ime-bound";
            if (field == null || field.ClassListContains(boundClass))
                return;

            field.AddToClassList(boundClass);
            field.RegisterCallback<FocusInEvent>(_ => Input.imeCompositionMode = IMECompositionMode.On);
            field.RegisterCallback<FocusOutEvent>(_ => Input.imeCompositionMode = IMECompositionMode.Auto);
        }

        void OnDestroy()
        {
            GameTablePanel?.Dispose();
            CardTableView.DestroyAllUnder(transform);
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
                if (_messageReceivedHandler != null)
                    _flow.Gateway.MessageReceived -= _messageReceivedHandler;
            }
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }
    }
}
