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
            bool inRoom = _flow.Flow == FlowState.RoomFlow || _flow.Flow == FlowState.WaitingRoom || state.Phase == GamePhase.WaitingRoom;
            bool showGame = _flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing;
            bool showReveal = state.Phase == GamePhase.RoundReveal;
            bool showOver = state.Phase == GamePhase.GameOver;

            RoomPanel.SetVisible(inRoom && !showGame);
            GameTablePanel.SetVisible(showGame || showReveal || showOver);

            if (inRoom) RoomPanel.Render();
            if (showGame) GameTablePanel.RenderGame();
            if (showReveal) GameTablePanel.RenderReveal();
            if (showOver) GameTablePanel.RenderGameOver();
        }

        void OnDestroy()
        {
            if (_flow != null) _flow.StateChanged -= OnStateChanged;
        }
    }
}
