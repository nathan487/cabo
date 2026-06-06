using UnityEngine;
using UnityEngine.UIElements;
using Cabo.Client.UI;

namespace Cabo.Client
{
    /// <summary>
    /// Self-initializing entry point. No scene setup required.
    /// Drop this script on any GameObject or let it auto-create via RuntimeInitializeOnLoadMethod.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 8888;

        NetworkGateway _gateway;
        GameFlow _flow;
        UIManager _ui;
        UIDocument _uiDoc;

        static GameBootstrap _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (_instance != null) return;
            var go = new GameObject("GameBootstrap");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<GameBootstrap>();
        }

        void Start()
        {
            if (_instance == null) _instance = this;

            // Create UIDocument
            var panelGO = new GameObject("UIDocument");
            panelGO.transform.SetParent(transform);
            _uiDoc = panelGO.AddComponent<UIDocument>();

            _gateway = new NetworkGateway();
            _flow = new GameFlow(_gateway);
            _gateway.MessageReceived += msg => _flow.State.UpdateFromMessage(msg);

            _ui = gameObject.AddComponent<UIManager>();
            _ui.uiDocument = _uiDoc;
            _ui.Initialize(_flow);

            var nickname = "Unity_" + Random.Range(100, 999);
            _flow.Connect(serverHost, serverPort, nickname);

            Debug.Log($"[GameBootstrap] Started — {serverHost}:{serverPort} as {nickname}");
        }

        void Update()
        {
            _flow?.Tick();
        }

        void OnDestroy()
        {
            _flow?.Dispose();
        }
    }
}
