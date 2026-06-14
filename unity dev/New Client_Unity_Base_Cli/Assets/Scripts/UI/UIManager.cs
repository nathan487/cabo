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
        [SerializeField] public UIDocument uiDocument;

        public VisualElement Root { get; private set; }
        public RoomPanel RoomPanel { get; private set; }
        public GameTablePanel GameTablePanel { get; private set; }

        GameFlow _flow;
        VisualElement _backgroundLayer;
        System.Action<Game.Messages.ServerMessage> _messageReceivedHandler;
        bool _waitingForRevealAnimationDrain;

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
                OnStateChanged();
        }

        void OnStateChanged()
        {
            var state = _flow.State;
            Debug.Log($"[UIManager] OnStateChanged: phase={state.Phase}, flow={_flow.Flow}, players={state.Players.Count}, room={state.RoomCode}");

            // Show/hide panels based on flow state
            bool showOver = state.Phase == GamePhase.GameOver;
            bool revealPending = !showOver && (state.Phase == GamePhase.RoundReveal || state.RoundJustRevealed || _flow.Flow == FlowState.RoundReveal);
            bool waitForActionAnimation = revealPending && GameTablePanel.HasPendingActionAnimation;
            _waitingForRevealAnimationDrain = waitForActionAnimation;
            bool showReveal = revealPending && !waitForActionAnimation;
            bool showGame = !showReveal && !showOver && (_flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing);
            if (waitForActionAnimation)
                showGame = true;
            bool showRoomPanel = _flow.Flow == FlowState.Home
                || _flow.Flow == FlowState.Connecting
                || _flow.Flow == FlowState.RoomFlow
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
