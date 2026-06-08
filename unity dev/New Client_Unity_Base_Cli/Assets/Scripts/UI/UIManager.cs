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
                Root.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
                Root.style.color = new Color(0.86f, 0.86f, 0.90f);
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
            bool showReveal = !showOver && (state.Phase == GamePhase.RoundReveal || state.RoundJustRevealed || _flow.Flow == FlowState.RoundReveal);
            bool showGame = !showReveal && !showOver && (_flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing);
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

            Root.style.color = new Color(0.86f, 0.86f, 0.90f);
            Root.style.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);

            Root.Query<Label>().ForEach(label =>
            {
                if (IsOnBrightSurface(label))
                    return;
                label.style.color = new Color(0.86f, 0.86f, 0.90f);
            });

            Root.Query<Button>().ForEach(button =>
            {
                button.style.color = new Color(0.92f, 0.92f, 0.96f);
                button.style.backgroundColor = new Color(0.20f, 0.20f, 0.32f);
                button.style.borderTopLeftRadius = 6;
                button.style.borderTopRightRadius = 6;
                button.style.borderBottomLeftRadius = 6;
                button.style.borderBottomRightRadius = 6;
                button.style.borderTopWidth = 1;
                button.style.borderRightWidth = 1;
                button.style.borderBottomWidth = 1;
                button.style.borderLeftWidth = 1;
                button.style.borderTopColor = new Color(0.39f, 0.39f, 0.59f);
                button.style.borderRightColor = new Color(0.39f, 0.39f, 0.59f);
                button.style.borderBottomColor = new Color(0.39f, 0.39f, 0.59f);
                button.style.borderLeftColor = new Color(0.39f, 0.39f, 0.59f);
                button.style.paddingTop = 8;
                button.style.paddingBottom = 8;
                button.style.paddingLeft = 14;
                button.style.paddingRight = 14;
                button.style.minWidth = 104;
                button.style.minHeight = 34;
                button.style.unityTextAlign = TextAnchor.MiddleCenter;
            });

            Root.Query<TextField>().ForEach(field =>
            {
                EnsureImeSupport(field);
                field.style.minWidth = 180;
                field.style.color = new Color(0.12f, 0.12f, 0.16f);
            });

            Root.Query<VisualElement>(className: "unity-text-field__input").ForEach(input =>
            {
                input.style.color = new Color(0.12f, 0.12f, 0.16f);
                input.style.backgroundColor = new Color(0.96f, 0.96f, 0.98f);
            });
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
