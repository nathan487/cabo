using UnityEngine;
using UnityEngine.UIElements;

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
            _flow = flow;
            EnsureRoot();

            // Build panels
            RoomPanel = new RoomPanel(Root, flow);
            GameTablePanel = new GameTablePanel(Root, flow);
            GameTablePanel.SetAnimationQueueDrainedCallback(OnStateChanged);

            // Listen for state changes
            flow.StateChanged += OnStateChanged;
            flow.Gateway.MessageReceived += _ => OnStateChanged();

            // Initial render
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
            bool showReveal = revealPending && !waitForActionAnimation;
            bool showGame = !showReveal && !showOver && (_flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing);
            if (waitForActionAnimation)
                showGame = true;
            bool showRoomPanel = _flow.Flow == FlowState.Home
                || _flow.Flow == FlowState.Connecting
                || _flow.Flow == FlowState.RoomFlow
                || _flow.Flow == FlowState.WaitingRoom
                || state.Phase == GamePhase.WaitingRoom;

            RoomPanel.SetVisible(showRoomPanel && !showGame && !showReveal && !showOver);
            GameTablePanel.SetVisible(showGame || showReveal || showOver);

            if (showRoomPanel && !showReveal && !showOver) RoomPanel.Render();
            if (showGame) GameTablePanel.RenderGame();
            if (showReveal) GameTablePanel.RenderReveal();
            if (showOver) GameTablePanel.RenderGameOver();

            ApplyRuntimeUiFallback();
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
                UITheme.ApplyInput(field);
            });

            Root.Query<VisualElement>(className: "unity-text-field__input").ForEach(input =>
            {
                UITheme.ApplyInputElement(input);
            });

            Root.Query<VisualElement>(className: "unity-base-field__input").ForEach(input =>
            {
                UITheme.ApplyInputElement(input);
            });

            Root.Query<VisualElement>(className: "unity-base-text-field__input").ForEach(input =>
            {
                UITheme.ApplyInputElement(input);
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
            if (_flow != null) _flow.StateChanged -= OnStateChanged;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }
    }
}
