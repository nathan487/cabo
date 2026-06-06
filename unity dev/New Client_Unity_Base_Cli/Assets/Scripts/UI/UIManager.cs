using UnityEngine;
using UnityEngine.UIElements;

namespace Cabo.Client.UI
{
    /// <summary>
    /// Root UI Manager. Owns the UIDocument and routes between panels.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;

        public VisualElement Root { get; private set; }
        public RoomPanel RoomPanel { get; private set; }
        public GameTablePanel GameTablePanel { get; private set; }

        GameFlow _flow;

        void Awake()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            Root = uiDocument.rootVisualElement;
        }

        public void Initialize(GameFlow flow)
        {
            _flow = flow;

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

            // Show/hide panels based on flow state
            bool showRoom = _flow.Flow == FlowState.WaitingRoom || state.Phase == GamePhase.WaitingRoom;
            bool showGame = _flow.Flow == FlowState.Playing || state.Phase == GamePhase.Playing;
            bool showReveal = state.Phase == GamePhase.RoundReveal;
            bool showOver = state.Phase == GamePhase.GameOver;

            RoomPanel.SetVisible(showRoom && !showGame);
            GameTablePanel.SetVisible(showGame || showReveal || showOver);

            if (showRoom) RoomPanel.Render();
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
